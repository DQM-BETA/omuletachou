using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Integrations.Social;

/// <summary>
/// Publisher do TikTok (Issue #10 / #77): publica videos de produtos aprovados via TikTok Content
/// Posting API v2, fluxo <c>FILE_UPLOAD</c> em 3 etapas (init do upload, upload chunked via PUT,
/// polling de status), com validacao client-side de duracao (<see cref="Mp4DurationReader"/>),
/// disclosure obrigatorio (<see cref="SocialDisclosureHelper"/>), renovacao reativa de
/// access_token em 401 e backoff exponencial local em 429.
///
/// Decisao de implementacao: chamadas HTTP diretas (<see cref="HttpClient"/>), sem SDK — mesmo
/// padrao ja usado pelo <see cref="YoutubePublisher"/>/<see cref="InstagramPublisher"/>.
/// </summary>
public class TikTokPublisher : ISocialPublisher
{
    private const string InitUrl = "https://open.tiktokapis.com/v2/post/publish/video/init/";
    private const string StatusUrl = "https://open.tiktokapis.com/v2/post/publish/status/fetch/";
    private const string RefreshUrl = "https://open.tiktokapis.com/v2/oauth/token/";

    private const string NoVideoErrorMessage = "Produto sem mídia de vídeo, não aplicável ao TikTok";
    private const string DefaultPrivacyLevel = "SELF_ONLY";
    private const int DefaultMinDurationSeconds = 3;
    private const int DefaultMaxDurationSeconds = 600;

    // 8MB — mesma ordem de grandeza recomendada pelo YoutubePublisher (upload resumable),
    // reaproveitada aqui para nao introduzir uma segunda constante de chunk divergente sem motivo.
    private const int ChunkSizeBytes = 8 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;
    private readonly ILogger<TikTokPublisher> _logger;
    private readonly TimeSpan[] _retryDelays;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _pollTimeout;

    public TikTokPublisher(
        HttpClient httpClient,
        AfiliadoBotDbContext dbContext,
        IMediaStorage mediaStorage,
        ILogger<TikTokPublisher> logger,
        TimeSpan[]? retryDelays = null,
        TimeSpan? pollInterval = null,
        TimeSpan? pollTimeout = null)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
        _logger = logger;
        _retryDelays = retryDelays ?? new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(15);
        _pollTimeout = pollTimeout ?? TimeSpan.FromMinutes(10);
    }

    public SocialNetwork Network => SocialNetwork.TikTok;

    public async Task<bool> PublishAsync(PublicationQueue item, CancellationToken ct = default)
    {
        var credentials = await LoadCredentialsAsync(ct);

        if (string.IsNullOrWhiteSpace(credentials.ClientKey) ||
            string.IsNullOrWhiteSpace(credentials.ClientSecret) ||
            string.IsNullOrWhiteSpace(credentials.RefreshToken) ||
            string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            throw new InvalidOperationException(
                "Credenciais do TikTok (tiktok.client_key, tiktok.client_secret, tiktok.refresh_token, tiktok.access_token) ausentes ou invalidas.");
        }

        var product = item.Product ?? await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);

        if (product is null)
            throw new InvalidOperationException($"Produto {item.ProductId} nao encontrado para publicacao no TikTok.");

        // Fallback de seguranca: mesmo com o fix do ProcessorJob, item legado ou falha de regra
        // pode chegar aqui sem midia de video — falhar sem retry.
        if (!HasVideoMedia(product))
        {
            _logger.LogWarning(
                "TikTokPublisher: produto {ProductId} sem midia de video (MediaType={MediaType}). Fallback de seguranca acionado.",
                product.Id, product.MediaType);
            FailPermanently(item, NoVideoErrorMessage);
            return false;
        }

        var (mediaPath, downloaded) = await ResolveMediaSourceAsync(product, ct);

        if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
        {
            _logger.LogWarning(
                "TikTokPublisher: nao foi possivel resolver midia de video local para o produto {ProductId}.",
                product.Id);
            FailPermanently(item, NoVideoErrorMessage);
            return false;
        }

        try
        {
            // Validacao de duracao (CA6-CA9), ANTES de qualquer chamada a API — client-side, sem
            // retry quando fora do intervalo aceito.
            if (!Mp4DurationReader.TryGetDurationSeconds(mediaPath, out var durationSeconds) ||
                durationSeconds < credentials.MinDurationSeconds ||
                durationSeconds > credentials.MaxDurationSeconds)
            {
                var maxMinutes = credentials.MaxDurationSeconds / 60;
                FailPermanently(
                    item,
                    $"Vídeo fora do intervalo de duração aceito pelo TikTok ({credentials.MinDurationSeconds}s-{maxMinutes}min)");
                return false;
            }

            var caption = SocialDisclosureHelper.AppendIfMissing(product.AiCaption ?? string.Empty);
            var accessToken = credentials.AccessToken;
            var fileLength = new FileInfo(mediaPath).Length;
            var totalChunks = (int)Math.Ceiling(fileLength / (double)ChunkSizeBytes);
            if (totalChunks == 0)
                totalChunks = 1;

            // Etapa 1: init do upload (CA1/CA11).
            var (initResponse, tokenAfterInit, refreshFailedInit) = await SendAuthorizedWithRetryAsync(
                token => BuildInitRequest(token, caption, credentials.PrivacyLevel, fileLength, ChunkSizeBytes, totalChunks),
                accessToken, credentials, ct);

            if (refreshFailedInit)
            {
                FailPermanently(item, "Falha ao renovar access_token do TikTok (refresh_token inválido ou revogado).");
                return false;
            }

            accessToken = tokenAfterInit;

            var initBody = await initResponse!.Content.ReadAsStringAsync(ct);

            if (!initResponse.IsSuccessStatusCode)
            {
                item.RegisterAttempt(false, $"Falha ao iniciar upload no TikTok (HTTP {(int)initResponse.StatusCode}): {initBody}");
                return false;
            }

            var (uploadUrl, publishId) = ParseInitResponse(initBody);

            if (string.IsNullOrWhiteSpace(uploadUrl) || string.IsNullOrWhiteSpace(publishId))
            {
                item.RegisterAttempt(false, $"Falha ao iniciar upload no TikTok: resposta sem upload_url/publish_id ({initBody}).");
                return false;
            }

            // Etapa 2: upload chunked via PUT (CA2).
            var (uploadOk, uploadError) = await UploadChunksAsync(uploadUrl, mediaPath, fileLength, ct);

            if (!uploadOk)
            {
                item.RegisterAttempt(false, uploadError ?? "Falha ao enviar video para o TikTok.");
                return false;
            }

            // Etapa 3: polling de status ate PUBLISH_COMPLETE (CA3/CA4/CA5).
            return await PollPublishStatusAsync(publishId, accessToken, credentials, item, ct);
        }
        finally
        {
            if (downloaded)
                TryDeleteTempFile(mediaPath);
        }
    }

    /// <summary>
    /// Forca o item a esgotar as tentativas de retry (RetryCount >= 3), garantindo que o
    /// <c>PublisherJob</c> nao o reprocesse — mesmo padrao do <see cref="YoutubePublisher"/>/
    /// <see cref="InstagramPublisher"/>.
    /// </summary>
    private static void FailPermanently(PublicationQueue item, string errorMessage)
    {
        while (item.RetryCount < 3)
            item.RegisterAttempt(false, errorMessage);
    }

    private static bool HasVideoMedia(Product product)
    {
        return string.Equals(product.MediaType, "video", StringComparison.OrdinalIgnoreCase) &&
               (!string.IsNullOrWhiteSpace(product.MediaLocalPath) || !string.IsNullOrWhiteSpace(product.MediaUrl));
    }

    private sealed record TikTokCredentials(
        string? ClientKey,
        string? ClientSecret,
        string? RefreshToken,
        string? AccessToken,
        string PrivacyLevel,
        int MinDurationSeconds,
        int MaxDurationSeconds);

    private async Task<TikTokCredentials> LoadCredentialsAsync(CancellationToken ct)
    {
        var keys = new[]
        {
            "tiktok.client_key",
            "tiktok.client_secret",
            "tiktok.refresh_token",
            "tiktok.access_token",
            "tiktok.privacy_level",
            "tiktok.min_duration_seconds",
            "tiktok.max_duration_seconds",
        };

        var values = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        values.TryGetValue("tiktok.client_key", out var clientKey);
        values.TryGetValue("tiktok.client_secret", out var clientSecret);
        values.TryGetValue("tiktok.refresh_token", out var refreshToken);
        values.TryGetValue("tiktok.access_token", out var accessToken);
        values.TryGetValue("tiktok.privacy_level", out var privacyLevel);
        values.TryGetValue("tiktok.min_duration_seconds", out var minDurationRaw);
        values.TryGetValue("tiktok.max_duration_seconds", out var maxDurationRaw);

        var minDuration = int.TryParse(minDurationRaw, out var min) ? min : DefaultMinDurationSeconds;
        var maxDuration = int.TryParse(maxDurationRaw, out var max) ? max : DefaultMaxDurationSeconds;

        return new TikTokCredentials(
            clientKey,
            clientSecret,
            refreshToken,
            accessToken,
            string.IsNullOrWhiteSpace(privacyLevel) ? DefaultPrivacyLevel : privacyLevel,
            minDuration,
            maxDuration);
    }

    /// <summary>
    /// Resolve a fonte de midia local: usa <see cref="Product.MediaLocalPath"/> quando presente e
    /// existente em disco; caso contrario baixa <see cref="Product.MediaUrl"/> via
    /// <see cref="IMediaStorage"/> para um arquivo local temporario. O fluxo FILE_UPLOAD exige os
    /// bytes do video local (diferente do Instagram, que usa video_url remoto).
    /// </summary>
    private async Task<(string? Path, bool Downloaded)> ResolveMediaSourceAsync(Product product, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(product.MediaLocalPath) && File.Exists(product.MediaLocalPath))
            return (product.MediaLocalPath, false);

        if (string.IsNullOrWhiteSpace(product.MediaUrl))
            return (null, false);

        var (localPath, _) = await _mediaStorage.DownloadAsync(product.MediaUrl, ct);
        return (localPath, localPath is not null);
    }

    private static void TryDeleteTempFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Limpeza best-effort — nao e requisito de aceite formal.
        }
    }

    private static HttpRequestMessage BuildInitRequest(
        string accessToken, string caption, string privacyLevel, long fileLength, int chunkSize, int totalChunks)
    {
        var payload = new
        {
            post_info = new
            {
                title = caption,
                privacy_level = privacyLevel,
                disable_duet = false,
                disable_comment = false,
                brand_content_toggle = true,
            },
            source_info = new
            {
                source = "FILE_UPLOAD",
                video_size = fileLength,
                chunk_size = chunkSize,
                total_chunk_count = totalChunks,
            },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, InitUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    private static (string? UploadUrl, string? PublishId) ParseInitResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return (null, null);

            var uploadUrl = data.TryGetProperty("upload_url", out var uploadUrlProp) ? uploadUrlProp.GetString() : null;
            var publishId = data.TryGetProperty("publish_id", out var publishIdProp) ? publishIdProp.GetString() : null;

            return (uploadUrl, publishId);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Etapa 2 (CA2): envia o arquivo em chunks fixos de <see cref="ChunkSizeBytes"/> para a
    /// upload URL retornada pela etapa 1, com <c>Content-Range</c>/<c>Content-Length</c>
    /// corretos por chunk. Sujeito a backoff de 429 (<see cref="SendWithRetryAsync"/>); qualquer
    /// outro status nao-2xx falha o chunk imediatamente.
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> UploadChunksAsync(
        string uploadUrl, string filePath, long fileLength, CancellationToken ct)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        long offset = 0;

        // Arquivo vazio (edge case defensivo): ainda assim envia um PUT com Content-Range 0-(-1)/0
        // nao faz sentido — nesse caso, nada a enviar, considerado sucesso (nao deveria ocorrer
        // apos validacao de duracao).
        if (fileLength == 0)
            return (true, null);

        while (offset < fileLength)
        {
            var chunkSize = (int)Math.Min(ChunkSizeBytes, fileLength - offset);
            var buffer = new byte[chunkSize];
            var read = await fileStream.ReadAsync(buffer.AsMemory(0, chunkSize), ct);

            if (read < chunkSize)
                Array.Resize(ref buffer, read);

            var end = offset + read - 1;
            var chunkOffset = offset;
            var chunkBuffer = buffer;

            var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
                {
                    Content = new ByteArrayContent(chunkBuffer),
                };
                request.Content.Headers.ContentRange = new ContentRangeHeaderValue(chunkOffset, end, fileLength);
                request.Content.Headers.ContentLength = chunkBuffer.Length;
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                return request;
            }, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return (false, $"Falha ao enviar chunk de video para o TikTok (HTTP {(int)response.StatusCode}): {body}");
            }

            offset += read;
        }

        return (true, null);
    }

    /// <summary>
    /// Etapa 3 (CA3/CA4/CA5): polling <c>POST /publish/status/fetch/</c> a cada
    /// <see cref="_pollInterval"/> ate <c>PUBLISH_COMPLETE</c> (sucesso) ou <c>FAILED</c> (falha
    /// imediata, com <see cref="ErrorMessage"/> descritivo). Timeout total (<see cref="_pollTimeout"/>)
    /// marca o item como <c>Failed</c> elegivel a retry (nao usa <see cref="FailPermanently"/>).
    /// </summary>
    private async Task<bool> PollPublishStatusAsync(
        string publishId, string accessToken, TikTokCredentials credentials, PublicationQueue item, CancellationToken ct)
    {
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pollCts.CancelAfter(_pollTimeout);

        while (true)
        {
            HttpResponseMessage? response;
            string tokenAfterPoll;
            bool refreshFailed;

            try
            {
                (response, tokenAfterPoll, refreshFailed) = await SendAuthorizedWithRetryAsync(
                    token => BuildStatusRequest(token, publishId), accessToken, credentials, pollCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                item.RegisterAttempt(false, "Timeout de 10 minutos aguardando publicacao do video no TikTok.");
                return false;
            }

            if (refreshFailed)
            {
                FailPermanently(item, "Falha ao renovar access_token do TikTok (refresh_token inválido ou revogado).");
                return false;
            }

            accessToken = tokenAfterPoll;

            if (response is null)
            {
                item.RegisterAttempt(false, "Timeout de 10 minutos aguardando publicacao do video no TikTok.");
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                item.RegisterAttempt(false, $"Falha ao consultar status de publicacao no TikTok (HTTP {(int)response.StatusCode}): {errorBody}");
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var status = ParseStatus(body);

            if (string.Equals(status, "PUBLISH_COMPLETE", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                item.RegisterAttempt(false, $"Publicacao do video falhou no TikTok (status={status}).");
                return false;
            }

            try
            {
                await Task.Delay(_pollInterval, pollCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                item.RegisterAttempt(false, "Timeout de 10 minutos aguardando publicacao do video no TikTok.");
                return false;
            }
        }
    }

    private static HttpRequestMessage BuildStatusRequest(string accessToken, string publishId)
    {
        var payload = new { publish_id = publishId };
        var request = new HttpRequestMessage(HttpMethod.Post, StatusUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    private static string? ParseStatus(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return null;

            return data.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Envia a requisicao construida por <paramref name="requestFactory"/> com backoff de 429
    /// (<see cref="SendWithRetryAsync"/>). Em 401, renova o access_token via
    /// <see cref="RefreshAccessTokenAsync"/> e repete a chamada original uma unica vez (CA16).
    /// Retorna <c>RefreshFailed = true</c> quando a renovacao falha (chamador deve
    /// <see cref="FailPermanently"/>).
    /// </summary>
    private async Task<(HttpResponseMessage? Response, string AccessToken, bool RefreshFailed)> SendAuthorizedWithRetryAsync(
        Func<string, HttpRequestMessage> requestFactory, string accessToken, TikTokCredentials credentials, CancellationToken ct)
    {
        var response = await SendWithRetryAsync(() => requestFactory(accessToken), ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return (response, accessToken, false);

        var (newAccessToken, newRefreshToken) = await RefreshAccessTokenAsync(
            credentials.ClientKey!, credentials.ClientSecret!, credentials.RefreshToken!, ct);

        if (string.IsNullOrWhiteSpace(newAccessToken))
            return (null, accessToken, true);

        await UpsertAppSettingAsync("tiktok.access_token", newAccessToken, ct);

        if (!string.IsNullOrWhiteSpace(newRefreshToken))
            await UpsertAppSettingAsync("tiktok.refresh_token", newRefreshToken, ct);

        response = await SendWithRetryAsync(() => requestFactory(newAccessToken), ct);

        return (response, newAccessToken, false);
    }

    /// <summary>
    /// Backoff exponencial local em 429 (CA17): 3 tentativas adicionais com delays fixos de
    /// 2s/4s/8s. Qualquer outro status HTTP nao-2xx e propagado como falha imediata (sem retry
    /// aqui) — mesmo padrao dos demais publishers.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(requestFactory(), ct);

        foreach (var delay in _retryDelays)
        {
            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                return response;

            await Task.Delay(delay, ct);
            response = await _httpClient.SendAsync(requestFactory(), ct);
        }

        return response;
    }

    /// <summary>
    /// Renova o access_token via <c>refresh_token</c> (CA16). Retorna null em qualquer falha
    /// (rede, HTTP nao-2xx ou resposta sem access_token).
    /// </summary>
    private async Task<(string? AccessToken, string? RefreshToken)> RefreshAccessTokenAsync(
        string clientKey, string clientSecret, string refreshToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_key"] = clientKey,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            }),
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TikTokPublisher: falha de rede ao renovar access_token.");
            return (null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "TikTokPublisher: resposta HTTP {StatusCode} ao renovar access_token.",
                (int)response.StatusCode);
            return (null, null);
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            var accessToken = doc.RootElement.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;
            var newRefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var refreshProp) ? refreshProp.GetString() : null;

            return string.IsNullOrWhiteSpace(accessToken) ? (null, null) : (accessToken, newRefreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TikTokPublisher: resposta invalida ao renovar access_token.");
            return (null, null);
        }
    }

    private async Task UpsertAppSettingAsync(string key, string value, CancellationToken ct)
    {
        var existing = await _dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

        if (existing is null)
            _dbContext.AppSettings.Add(new AppSetting(key, value));
        else
            existing.UpdateValue(value);

        await _dbContext.SaveChangesAsync(ct);
    }
}
