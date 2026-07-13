using System.Globalization;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Integrations.Social;

/// <summary>
/// Publisher do Instagram (Issue #9 / #73): publica videos de produtos aprovados como Reels via
/// Meta Graph API, em 3 etapas (criacao do container de midia, polling de status, publicacao do
/// container), com renovacao automatica de access_token via <c>fb_exchange_token</c> e disclosure
/// obrigatorio (<c>#publi</c>/<c>#publicidade</c>) anexado deterministicamente a legenda.
///
/// Decisao de implementacao: chamadas HTTP diretas (<see cref="HttpClient"/>), sem SDK — mesmo
/// padrao ja usado pelo <see cref="YoutubePublisher"/>/<see cref="TelegramPublisher"/>.
/// </summary>
public class InstagramPublisher : ISocialPublisher
{
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v21.0";
    private const string NoVideoErrorMessage = "Produto sem mídia de vídeo, não aplicável ao Instagram";

    private static readonly TimeSpan RenewalMargin = TimeSpan.FromDays(7);

    private readonly HttpClient _httpClient;
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly ILogger<InstagramPublisher> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _pollTimeout;

    public InstagramPublisher(
        HttpClient httpClient,
        AfiliadoBotDbContext dbContext,
        ILogger<InstagramPublisher> logger,
        TimeSpan? pollInterval = null,
        TimeSpan? pollTimeout = null)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(3);
        _pollTimeout = pollTimeout ?? TimeSpan.FromMinutes(2);
    }

    public SocialNetwork Network => SocialNetwork.Instagram;

    public async Task<bool> PublishAsync(PublicationQueue item, CancellationToken ct = default)
    {
        var (accessToken, appId, appSecret, pageId, tokenExpiresAt) = await LoadCredentialsAsync(ct);

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(pageId))
        {
            throw new InvalidOperationException(
                "Credenciais do Instagram (instagram.access_token, instagram.page_id) ausentes ou invalidas.");
        }

        var product = item.Product ?? await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);

        if (product is null)
            throw new InvalidOperationException($"Produto {item.ProductId} nao encontrado para publicacao no Instagram.");

        // Fallback de seguranca (CA15): mesmo com o fix do ProcessorJob, item legado ou falha de
        // regra pode chegar aqui sem midia de video — falhar sem retry.
        if (!HasVideoMedia(product))
        {
            _logger.LogWarning(
                "InstagramPublisher: produto {ProductId} sem midia de video (MediaType={MediaType}). Fallback de seguranca acionado.",
                product.Id, product.MediaType);
            FailPermanently(item, NoVideoErrorMessage);
            return false;
        }

        // Renovacao de token (CA12/CA13/CA14): margem de 7 dias antes da expiracao.
        if (NeedsRenewal(tokenExpiresAt))
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            {
                _logger.LogWarning(
                    "InstagramPublisher: renovacao de access_token necessaria, mas instagram.app_id/instagram.app_secret ausentes.");
                await UpsertAppSettingAsync("instagram.token_invalid", "true", ct);
                FailPermanently(item, "Falha ao renovar access_token do Instagram (app_id/app_secret ausentes).");
                return false;
            }

            var (newToken, newExpiresAt) = await RenewTokenAsync(accessToken, appId, appSecret, ct);

            if (string.IsNullOrWhiteSpace(newToken) || newExpiresAt is null)
            {
                _logger.LogWarning(
                    "InstagramPublisher: falha ao renovar access_token via fb_exchange_token para o produto {ProductId}.",
                    product.Id);
                await UpsertAppSettingAsync("instagram.token_invalid", "true", ct);
                FailPermanently(item, "Falha ao renovar access_token do Instagram (fb_exchange_token invalido ou revogado).");
                return false;
            }

            await UpsertAppSettingAsync("instagram.access_token", newToken, ct);
            await UpsertAppSettingAsync("instagram.token_expires_at", newExpiresAt.Value.ToString("O", CultureInfo.InvariantCulture), ct);
            accessToken = newToken;
        }

        // Resolucao de midia publica (CA6/CA7/CA8): video_url, nao download.
        var videoUrl = await ResolveVideoUrlAsync(product, ct);

        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            _logger.LogWarning(
                "InstagramPublisher: nao foi possivel resolver uma URL publica de video para o produto {ProductId}.",
                product.Id);
            item.RegisterAttempt(false, "URL de midia nao acessivel publicamente para publicacao no Instagram.");
            return false;
        }

        var caption = SocialDisclosureHelper.AppendIfMissing(product.AiCaption ?? string.Empty);

        // Etapa 1: criacao do container de midia (CA1).
        var (creationId, createError) = await CreateMediaContainerAsync(videoUrl, caption, pageId, accessToken, ct);

        if (string.IsNullOrWhiteSpace(creationId))
        {
            item.RegisterAttempt(false, createError ?? "Falha ao criar container de midia no Instagram.");
            return false;
        }

        // Etapa 2: polling de status ate FINISHED (CA2/CA4/CA5).
        var pollResult = await PollContainerStatusAsync(creationId, accessToken, item, ct);

        if (!pollResult)
            return false;

        // Etapa 3: publicacao do container (CA3).
        var (published, publishError) = await PublishContainerAsync(creationId, pageId, accessToken, ct);

        if (!published)
        {
            item.RegisterAttempt(false, publishError ?? "Falha ao publicar container no Instagram.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Forca o item a esgotar as tentativas de retry (RetryCount >= 3) — usado nos dois cenarios
    /// de falha "sem retry" explicitos: produto sem video (CA15) e falha de renovacao de token
    /// (CA14). Mesmo padrao do <see cref="YoutubePublisher"/>.
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

    private async Task<(string? AccessToken, string? AppId, string? AppSecret, string? PageId, string? TokenExpiresAt)> LoadCredentialsAsync(
        CancellationToken ct)
    {
        var keys = new[]
        {
            "instagram.access_token",
            "instagram.app_id",
            "instagram.app_secret",
            "instagram.page_id",
            "instagram.token_expires_at",
        };

        var values = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        values.TryGetValue("instagram.access_token", out var accessToken);
        values.TryGetValue("instagram.app_id", out var appId);
        values.TryGetValue("instagram.app_secret", out var appSecret);
        values.TryGetValue("instagram.page_id", out var pageId);
        values.TryGetValue("instagram.token_expires_at", out var tokenExpiresAt);

        return (accessToken, appId, appSecret, pageId, tokenExpiresAt);
    }

    /// <summary>
    /// CA12/CA13: renova quando restam menos de 7 dias de validade. Ausencia/valor invalido de
    /// <c>instagram.token_expires_at</c> e tratado como "precisa renovar" (por seguranca).
    /// </summary>
    private static bool NeedsRenewal(string? tokenExpiresAt)
    {
        if (!DateTime.TryParse(
                tokenExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var expiresAt))
        {
            return true;
        }

        return expiresAt - DateTime.UtcNow < RenewalMargin;
    }

    /// <summary>
    /// Renova o access_token via <c>fb_exchange_token</c> (CA12). Retorna null em qualquer falha
    /// (rede, HTTP nao-2xx ou resposta sem access_token/expires_in) — CA14.
    /// </summary>
    private async Task<(string? AccessToken, DateTime? ExpiresAt)> RenewTokenAsync(
        string currentToken, string appId, string appSecret, CancellationToken ct)
    {
        var url =
            $"{GraphApiBaseUrl}/oauth/access_token?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(appId)}" +
            $"&client_secret={Uri.EscapeDataString(appSecret)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(currentToken)}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "InstagramPublisher: falha de rede ao renovar access_token.");
            return (null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "InstagramPublisher: resposta HTTP {StatusCode} ao renovar access_token.",
                (int)response.StatusCode);
            return (null, null);
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            var accessToken = doc.RootElement.TryGetProperty("access_token", out var tokenProp)
                ? tokenProp.GetString()
                : null;

            long? expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expProp) && expProp.TryGetInt64(out var seconds)
                ? seconds
                : null;

            if (string.IsNullOrWhiteSpace(accessToken) || expiresIn is null)
                return (null, null);

            return (accessToken, DateTime.UtcNow.AddSeconds(expiresIn.Value));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InstagramPublisher: resposta invalida ao renovar access_token.");
            return (null, null);
        }
    }

    /// <summary>
    /// Resolve a URL publica do video: <see cref="Product.MediaLocalPath"/> (via
    /// <c>api.public_base_url</c> configurada) tem prioridade; na ausencia, cai para
    /// <see cref="Product.MediaUrl"/> (CA6/CA7). Retorna null quando nenhuma fonte e resolviva
    /// (CA8).
    /// </summary>
    private async Task<string?> ResolveVideoUrlAsync(Product product, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(product.MediaLocalPath))
        {
            var baseUrl = await _dbContext.AppSettings
                .Where(s => s.Key == "api.public_base_url")
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var fileName = Path.GetFileName(product.MediaLocalPath);
                return $"{baseUrl.TrimEnd('/')}/media/{fileName}";
            }
        }

        return string.IsNullOrWhiteSpace(product.MediaUrl) ? null : product.MediaUrl;
    }

    /// <summary>
    /// Etapa 1 (CA1): <c>POST /{ig-user-id}/media</c>. Nao-2xx retorna falha imediata sem
    /// prosseguir para o polling.
    /// </summary>
    private async Task<(string? CreationId, string? ErrorMessage)> CreateMediaContainerAsync(
        string videoUrl, string caption, string pageId, string accessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{GraphApiBaseUrl}/{pageId}/media")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["media_type"] = "REELS",
                ["video_url"] = videoUrl,
                ["caption"] = caption,
                ["access_token"] = accessToken,
            }),
        };

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return (null, $"Falha ao criar container de midia no Instagram (HTTP {(int)response.StatusCode}): {body}");

        string? creationId;
        try
        {
            using var doc = JsonDocument.Parse(body);
            creationId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        }
        catch (JsonException)
        {
            return (null, $"Falha ao criar container de midia no Instagram: resposta invalida ({body}).");
        }

        return string.IsNullOrWhiteSpace(creationId)
            ? (null, "Falha ao criar container de midia no Instagram: resposta sem id.")
            : (creationId, null);
    }

    /// <summary>
    /// Etapa 2 (CA2/CA4/CA5): polling a intervalos fixos de <see cref="_pollInterval"/> ate
    /// <c>status_code = FINISHED</c>. <c>FAILED</c>/<c>ERROR</c>/<c>EXPIRED</c> falham
    /// imediatamente. Timeout total (<see cref="_pollTimeout"/>) sem <c>FINISHED</c> falha com
    /// retry simples (diferente de <see cref="FailPermanently"/>).
    /// </summary>
    private async Task<bool> PollContainerStatusAsync(string creationId, string accessToken, PublicationQueue item, CancellationToken ct)
    {
        var statusUrl = $"{GraphApiBaseUrl}/{creationId}?fields=status_code&access_token={Uri.EscapeDataString(accessToken)}";

        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pollCts.CancelAfter(_pollTimeout);

        while (true)
        {
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(statusUrl, pollCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                item.RegisterAttempt(false, "Timeout de 2 minutos aguardando processamento do container de midia no Instagram.");
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                item.RegisterAttempt(false, $"Falha ao consultar status do container de midia no Instagram (HTTP {(int)response.StatusCode}): {errorBody}");
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            string? statusCode;
            try
            {
                using var doc = JsonDocument.Parse(body);
                statusCode = doc.RootElement.TryGetProperty("status_code", out var scProp) ? scProp.GetString() : null;
            }
            catch (JsonException)
            {
                item.RegisterAttempt(false, $"Falha ao consultar status do container de midia no Instagram: resposta invalida ({body}).");
                return false;
            }

            if (string.Equals(statusCode, "FINISHED", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(statusCode, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(statusCode, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(statusCode, "EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                item.RegisterAttempt(false, $"Processamento do container de midia falhou no Instagram (status_code={statusCode}).");
                return false;
            }

            try
            {
                await Task.Delay(_pollInterval, pollCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                item.RegisterAttempt(false, "Timeout de 2 minutos aguardando processamento do container de midia no Instagram.");
                return false;
            }
        }
    }

    /// <summary>
    /// Etapa 3 (CA3): <c>POST /{ig-user-id}/media_publish</c>.
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> PublishContainerAsync(
        string creationId, string pageId, string accessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{GraphApiBaseUrl}/{pageId}/media_publish")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["creation_id"] = creationId,
                ["access_token"] = accessToken,
            }),
        };

        var response = await _httpClient.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
            return (true, null);

        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, $"Falha ao publicar container no Instagram (HTTP {(int)response.StatusCode}): {body}");
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
