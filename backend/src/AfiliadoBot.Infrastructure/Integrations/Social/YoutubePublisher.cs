using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Integrations.Social;

/// <summary>
/// Publisher do YouTube (Issue #8 / #65): publica videos de produtos aprovados como YouTube
/// Shorts via YouTube Data API v3, com upload resumable em chunks de 8MB e renovacao
/// automatica de access_token via refresh_token (OAuth2).
///
/// Decisao de implementacao: chamadas HTTP diretas (<see cref="HttpClient"/>) em vez do SDK
/// oficial <c>Google.Apis.YouTube.v3</c> — o SDK nao expoe controle fino sobre o chunking do
/// upload resumable exigido pelos criterios de aceite (chunks fixos de 8MB, timeout por chunk
/// e timeout total), enquanto a chamada manual segue o mesmo padrao HTTP ja usado no projeto
/// (ex.: <c>ProcessorJob.EnsureAffiliateLinkAsync</c>, <c>TelegramPublisher</c>).
/// </summary>
public class YoutubePublisher : ISocialPublisher
{
    private const string OAuthTokenUrl = "https://oauth2.googleapis.com/token";
    private const string UploadBaseUrl = "https://www.googleapis.com/upload/youtube/v3/videos";
    private const string NoVideoErrorMessage = "Produto sem mídia de vídeo, não aplicável ao YouTube";

    private const int ChunkSizeBytes = 8 * 1024 * 1024; // 8MB, multiplo de 256KB (recomendacao Google).
    private static readonly TimeSpan ChunkTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromMinutes(15);

    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Eletrônicos"] = "28",
        ["Casa e Cozinha"] = "26",
        ["Beleza e Cuidados Pessoais"] = "26",
        ["Moda"] = "26",
        ["Brinquedos"] = "24",
        ["Geral"] = "22",
    };
    private const string DefaultCategoryId = "22";

    private readonly HttpClient _httpClient;
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;
    private readonly ILogger<YoutubePublisher> _logger;

    public YoutubePublisher(
        HttpClient httpClient,
        AfiliadoBotDbContext dbContext,
        IMediaStorage mediaStorage,
        ILogger<YoutubePublisher> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
        _logger = logger;
    }

    public SocialNetwork Network => SocialNetwork.Youtube;

    public async Task<bool> PublishAsync(PublicationQueue item, CancellationToken ct = default)
    {
        var (clientId, clientSecret, refreshToken) = await LoadCredentialsAsync(ct);

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException(
                "Credenciais do YouTube (youtube.client_id, youtube.client_secret, youtube.refresh_token) ausentes ou invalidas.");
        }

        var product = item.Product ?? await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);

        if (product is null)
            throw new InvalidOperationException($"Produto {item.ProductId} nao encontrado para publicacao no YouTube.");

        // Fallback de seguranca (CA16): mesmo com o fix do ProcessorJob, item legado ou falha
        // de regra pode chegar aqui sem midia de video — falhar sem retry.
        if (!string.Equals(product.MediaType, "video", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "YoutubePublisher: produto {ProductId} sem midia de video (MediaType={MediaType}). Fallback de seguranca acionado.",
                product.Id, product.MediaType);
            FailPermanently(item, NoVideoErrorMessage);
            return false;
        }

        var accessToken = await RefreshAccessTokenAsync(clientId, clientSecret, refreshToken, ct);

        if (accessToken is null)
        {
            _logger.LogWarning(
                "YoutubePublisher: falha ao renovar access_token via refresh_token para o produto {ProductId}.",
                product.Id);
            await UpsertAppSettingAsync("youtube.token_invalid", "true", ct);
            FailPermanently(item, "Falha ao renovar access_token do YouTube (refresh_token invalido ou revogado).");
            return false;
        }

        var (mediaPath, downloaded) = await ResolveMediaSourceAsync(product, ct);

        if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
        {
            _logger.LogWarning(
                "YoutubePublisher: nao foi possivel resolver midia de video para o produto {ProductId}.",
                product.Id);
            FailPermanently(item, NoVideoErrorMessage);
            return false;
        }

        try
        {
            var metadataJson = BuildMetadataJson(product);
            var fileLength = new FileInfo(mediaPath).Length;

            using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            totalCts.CancelAfter(TotalTimeout);

            var uploadUrl = await InitiateResumableUploadAsync(accessToken, metadataJson, fileLength, ct, totalCts);

            if (string.IsNullOrWhiteSpace(uploadUrl))
                throw new InvalidOperationException("YoutubePublisher: nao foi possivel iniciar upload resumable (Location ausente na resposta).");

            return await UploadChunksAsync(uploadUrl, mediaPath, fileLength, ct, totalCts);
        }
        finally
        {
            if (downloaded)
                TryDeleteTempFile(mediaPath);
        }
    }

    /// <summary>
    /// Forca o item a esgotar as tentativas de retry (RetryCount >= 3), garantindo que o
    /// <c>PublisherJob</c> nao o reprocesse — usado nos dois cenarios de falha "sem retry"
    /// explicitos do PRD: produto sem video (CA16) e refresh_token invalido (CA12).
    /// </summary>
    private static void FailPermanently(PublicationQueue item, string errorMessage)
    {
        while (item.RetryCount < 3)
            item.RegisterAttempt(false, errorMessage);
    }

    private async Task<(string? ClientId, string? ClientSecret, string? RefreshToken)> LoadCredentialsAsync(CancellationToken ct)
    {
        var keys = new[] { "youtube.client_id", "youtube.client_secret", "youtube.refresh_token" };

        var values = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        values.TryGetValue("youtube.client_id", out var clientId);
        values.TryGetValue("youtube.client_secret", out var clientSecret);
        values.TryGetValue("youtube.refresh_token", out var refreshToken);

        return (clientId, clientSecret, refreshToken);
    }

    /// <summary>
    /// Renova o access_token via refresh_token (CA11). Retorna null em qualquer falha
    /// (rede, HTTP nao-2xx ou resposta sem access_token) — CA12.
    /// </summary>
    private async Task<string?> RefreshAccessTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
            }),
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "YoutubePublisher: falha de rede ao renovar access_token.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "YoutubePublisher: resposta HTTP {StatusCode} ao renovar access_token.",
                (int)response.StatusCode);
            return null;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var accessToken = doc.RootElement.TryGetProperty("access_token", out var tokenProp)
                ? tokenProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(accessToken))
                return null;

            await UpsertAppSettingAsync("youtube.access_token", accessToken, ct);
            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YoutubePublisher: resposta invalida ao renovar access_token.");
            return null;
        }
    }

    /// <summary>
    /// Resolve a fonte de midia: usa <see cref="Product.MediaLocalPath"/> quando presente e
    /// existente em disco; caso contrario baixa <see cref="Product.MediaUrl"/> via
    /// <see cref="IMediaStorage"/> para um arquivo local temporario (CA2).
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
            // Limpeza best-effort — nao e requisito de aceite formal (ver tasks.md).
        }
    }

    private static string BuildMetadataJson(Product product)
    {
        var title = product.Title.Length > 100 ? product.Title[..100] : product.Title;
        var description = product.AiCaption ?? string.Empty;
        var categoryId = CategoryMap.TryGetValue(product.Category, out var mapped) ? mapped : DefaultCategoryId;

        var metadata = new
        {
            snippet = new
            {
                title,
                description,
                tags = new[] { "oferta", "desconto", "promocao", "youtube" },
                categoryId,
            },
            status = new
            {
                privacyStatus = "public",
            },
        };

        return JsonSerializer.Serialize(metadata);
    }

    /// <summary>
    /// POST inicial do fluxo de upload resumable — retorna a upload URL (header Location).
    /// </summary>
    private async Task<string?> InitiateResumableUploadAsync(
        string accessToken, string metadataJson, long fileLength, CancellationToken ct, CancellationTokenSource totalCts)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{UploadBaseUrl}?uploadType=resumable&part=snippet,status")
        {
            Content = new StringContent(metadataJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Upload-Content-Type", "video/*");
        request.Headers.TryAddWithoutValidation("X-Upload-Content-Length", fileLength.ToString());

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, totalCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout interno (CTS de 15min) — nao e cancelamento externo real, entao
            // convertemos para excecao "comum" para cair no retry padrao do PublisherJob.
            throw new InvalidOperationException("YoutubePublisher: timeout total (15 min) excedido ao iniciar upload resumable.");
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"YoutubePublisher: falha ao iniciar upload resumable (HTTP {(int)response.StatusCode}).");

        if (response.Headers.Location is not null)
            return response.Headers.Location.ToString();

        return response.Headers.TryGetValues("Location", out var values) ? values.FirstOrDefault() : null;
    }

    /// <summary>
    /// Envia o arquivo em chunks fixos de <see cref="ChunkSizeBytes"/> (8MB) para a upload URL,
    /// respeitando timeout por chunk (5min) e total (15min) — CA13/CA14/CA15.
    /// </summary>
    private async Task<bool> UploadChunksAsync(
        string uploadUrl, string filePath, long fileLength, CancellationToken ct, CancellationTokenSource totalCts)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        long offset = 0;

        while (offset < fileLength)
        {
            var chunkSize = (int)Math.Min(ChunkSizeBytes, fileLength - offset);
            var buffer = new byte[chunkSize];
            var read = await fileStream.ReadAsync(buffer.AsMemory(0, chunkSize), ct);

            if (read < chunkSize)
                Array.Resize(ref buffer, read);

            var end = offset + read - 1;

            using var chunkCts = CancellationTokenSource.CreateLinkedTokenSource(totalCts.Token);
            chunkCts.CancelAfter(ChunkTimeout);

            var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = new ByteArrayContent(buffer),
            };
            request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, end, fileLength);
            request.Content.Headers.ContentLength = buffer.Length;

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, chunkCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                var isTotalTimeout = totalCts.IsCancellationRequested;
                throw new InvalidOperationException(isTotalTimeout
                    ? "YoutubePublisher: timeout total (15 min) excedido durante upload de video."
                    : "YoutubePublisher: timeout de chunk (5 min) excedido durante upload de video.");
            }

            // 308 Resume Incomplete: chunk intermediario aceito, prossegue para o proximo.
            if ((int)response.StatusCode == 308)
            {
                offset += read;
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"YoutubePublisher: falha ao enviar chunk de video (HTTP {(int)response.StatusCode}).");

            // Ultimo chunk confirmado pelo YouTube (200/201).
            return true;
        }

        return true;
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
