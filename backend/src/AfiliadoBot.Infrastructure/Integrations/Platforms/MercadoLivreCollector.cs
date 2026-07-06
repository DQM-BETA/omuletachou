using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Integrations.Platforms;

/// <summary>
/// Collector do MercadoLivre. Autentica via OAuth2 client_credentials com cache/refresh
/// automatico de token em app_settings, busca produtos mais vendidos, faz upsert por
/// (Platform, ExternalId) e aciona scoring automatico via IAiService para produtos novos.
/// AffiliateLink NAO e preenchido na coleta (fica null ate aprovacao pelo ProcessorJob, Issue #6).
/// </summary>
public class MercadoLivreCollector : IPlatformCollector
{
    private const string OAuthUrl = "https://api.mercadolibre.com/oauth/token";
    private const string SearchUrl = "https://api.mercadolibre.com/sites/MLB/search?sort=best_seller&limit=20";
    private const string DefaultCategory = "Geral";
    private const int RateLimitDelayMs = 150;
    private static readonly TimeSpan TokenExpiryMargin = TimeSpan.FromMinutes(5);

    private static readonly int[] RetryDelaysMs = { 2000, 4000, 8000 };

    private readonly HttpClient _httpClient;
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly IAiService _aiService;
    private readonly ILogger<MercadoLivreCollector> _logger;

    public MercadoLivreCollector(
        HttpClient httpClient,
        AfiliadoBotDbContext dbContext,
        IAiService aiService,
        ILogger<MercadoLivreCollector> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _aiService = aiService;
        _logger = logger;
    }

    public Platform Platform => Platform.MercadoLivre;

    public async Task<IEnumerable<Product>> CollectAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);

        ValidateCredentials(settings);

        var accessToken = await EnsureValidTokenAsync(settings, ct);

        if (accessToken is null)
        {
            // Falha ao obter token — ciclo abortado sem exception (ja logado em EnsureValidTokenAsync).
            return new List<Product>();
        }

        await Task.Delay(RateLimitDelayMs, ct);

        var response = await SendWithRetryAsync(accessToken, ct);

        var collected = new List<Product>();

        if (response is null)
        {
            // Retry esgotado ou falha nao-429 — ciclo abortado sem exception.
            return collected;
        }

        var items = ParseItems(response);

        foreach (var item in items)
        {
            var product = await UpsertProductAsync(item, ct);
            if (product is not null)
                collected.Add(product);
        }

        await _dbContext.SaveChangesAsync(ct);

        return collected;
    }

    private async Task<MercadoLivreSettings> LoadSettingsAsync(CancellationToken ct)
    {
        var keys = new[]
        {
            "mercadolivre.client_id",
            "mercadolivre.client_secret",
            "mercadolivre.access_token",
            "mercadolivre.token_expires_at"
        };

        var values = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        values.TryGetValue("mercadolivre.client_id", out var clientId);
        values.TryGetValue("mercadolivre.client_secret", out var clientSecret);
        values.TryGetValue("mercadolivre.access_token", out var accessToken);
        values.TryGetValue("mercadolivre.token_expires_at", out var tokenExpiresAtRaw);

        DateTime? tokenExpiresAt = null;
        if (!string.IsNullOrWhiteSpace(tokenExpiresAtRaw) &&
            DateTime.TryParse(
                tokenExpiresAtRaw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            tokenExpiresAt = parsed;
        }

        return new MercadoLivreSettings(
            clientId ?? string.Empty,
            clientSecret ?? string.Empty,
            accessToken,
            tokenExpiresAt);
    }

    private static void ValidateCredentials(MercadoLivreSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new InvalidOperationException("Credencial ausente: mercadolivre.client_id");

        if (string.IsNullOrWhiteSpace(settings.ClientSecret))
            throw new InvalidOperationException("Credencial ausente: mercadolivre.client_secret");
    }

    private async Task<string?> EnsureValidTokenAsync(MercadoLivreSettings settings, CancellationToken ct)
    {
        var isValid = !string.IsNullOrWhiteSpace(settings.AccessToken) &&
                      settings.TokenExpiresAt.HasValue &&
                      settings.TokenExpiresAt.Value > DateTime.UtcNow.Add(TokenExpiryMargin);

        if (isValid)
        {
            return settings.AccessToken;
        }

        return await RequestNewTokenAsync(settings, ct);
    }

    private async Task<string?> RequestNewTokenAsync(MercadoLivreSettings settings, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, OAuthUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MercadoLivreCollector: falha de rede ao solicitar token OAuth2. Ciclo abortado sem exception.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "MercadoLivreCollector: resposta HTTP {StatusCode} ao solicitar token OAuth2. Ciclo abortado sem exception.",
                (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(body);
        var accessToken = doc.RootElement.TryGetProperty("access_token", out var tokenProp)
            ? tokenProp.GetString()
            : null;

        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresProp)
            ? expiresProp.GetInt32()
            : 0;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("MercadoLivreCollector: resposta de token OAuth2 sem access_token. Ciclo abortado sem exception.");
            return null;
        }

        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        await PersistTokenAsync(accessToken, expiresAt, ct);

        return accessToken;
    }

    private async Task PersistTokenAsync(string accessToken, DateTime expiresAt, CancellationToken ct)
    {
        var expiresAtStr = expiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        await UpsertSettingAsync("mercadolivre.access_token", accessToken, ct);
        await UpsertSettingAsync("mercadolivre.token_expires_at", expiresAtStr, ct);

        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task UpsertSettingAsync(string key, string value, CancellationToken ct)
    {
        var existing = await _dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

        if (existing is not null)
        {
            existing.UpdateValue(value);
        }
        else
        {
            _dbContext.AppSettings.Add(new AppSetting(key, value));
        }
    }

    private async Task<string?> SendWithRetryAsync(string accessToken, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, SearchUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "MercadoLivreCollector: falha de rede na busca de produtos. Ciclo abortado sem exception.");
                return null;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt < RetryDelaysMs.Length)
                {
                    await Task.Delay(RetryDelaysMs[attempt], ct);
                    continue;
                }

                _logger.LogWarning(
                    "MercadoLivreCollector: rate limit (429) apos {Attempts} tentativas. Ciclo abortado sem exception.",
                    RetryDelaysMs.Length + 1);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MercadoLivreCollector: resposta HTTP {StatusCode} na busca de produtos. Ciclo abortado sem exception.",
                    (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }

        return null;
    }

    private static List<MercadoLivreItem> ParseItems(string responseBody)
    {
        var items = new List<MercadoLivreItem>();

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var item in results.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty;

            decimal salePrice = 0;
            if (item.TryGetProperty("price", out var priceProp))
            {
                salePrice = priceProp.GetDecimal();
            }

            decimal? originalPrice = null;
            if (item.TryGetProperty("original_price", out var originalPriceProp) &&
                originalPriceProp.ValueKind != JsonValueKind.Null)
            {
                originalPrice = originalPriceProp.GetDecimal();
            }

            string? thumbnail = null;
            if (item.TryGetProperty("thumbnail", out var thumbnailProp))
            {
                thumbnail = thumbnailProp.GetString();
            }

            decimal? discount = null;
            if (item.TryGetProperty("discount", out var discountProp) &&
                discountProp.ValueKind != JsonValueKind.Null)
            {
                discount = discountProp.GetDecimal();
            }

            var finalOriginalPrice = originalPrice ?? salePrice;
            var discountPct = discount ?? (finalOriginalPrice > 0
                ? Math.Round((1 - (salePrice / finalOriginalPrice)) * 100, 2)
                : 0);

            if (discountPct < 0) discountPct = 0;
            if (discountPct > 100) discountPct = 100;

            items.Add(new MercadoLivreItem(id!, title, salePrice, finalOriginalPrice, discountPct, thumbnail));
        }

        return items;
    }

    private async Task<Product?> UpsertProductAsync(MercadoLivreItem item, CancellationToken ct)
    {
        var existing = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Platform == Platform.MercadoLivre && p.ExternalId == item.Id, ct);

        if (existing is not null)
        {
            existing.UpdateFromCollector(
                item.SalePrice,
                item.OriginalPrice,
                item.DiscountPct,
                imageUrl: null,
                mediaUrl: item.Thumbnail,
                mediaType: item.Thumbnail is not null ? "image" : null);
            return existing;
        }

        var slug = GenerateSlug(item.Title, item.Id);

        var product = new Product(
            title: item.Title,
            description: item.Title,
            salePrice: item.SalePrice,
            originalPrice: item.OriginalPrice,
            discountPct: item.DiscountPct,
            affiliateLink: null,
            slug: slug,
            category: DefaultCategory,
            platform: Platform.MercadoLivre,
            externalId: item.Id,
            mediaUrl: item.Thumbnail,
            mediaType: item.Thumbnail is not null ? "image" : null);

        _dbContext.Products.Add(product);

        var score = await _aiService.ScoreProductAsync(product, ct);
        product.UpdateAiResult(score.Score, score.Reason, string.Empty);

        return product;
    }

    private static string GenerateSlug(string title, string externalId)
    {
        var baseSlug = title.ToLowerInvariant().Trim();

        var sb = new StringBuilder();
        foreach (var c in baseSlug)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }

        var slugBase = sb.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(slugBase))
            slugBase = "produto";

        return $"{slugBase}-{externalId.ToLowerInvariant()}";
    }

    private record MercadoLivreSettings(
        string ClientId,
        string ClientSecret,
        string? AccessToken,
        DateTime? TokenExpiresAt);

    private record MercadoLivreItem(
        string Id,
        string Title,
        decimal SalePrice,
        decimal OriginalPrice,
        decimal DiscountPct,
        string? Thumbnail);
}
