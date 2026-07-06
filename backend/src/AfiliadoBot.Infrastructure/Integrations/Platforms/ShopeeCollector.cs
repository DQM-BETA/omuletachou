using System.Net;
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
/// Collector da Shopee Affiliate API. Autentica via HMAC-SHA256, consulta produtos via
/// GraphQL (getProducts), faz upsert por (Platform, ExternalId) e aciona scoring automatico
/// via IAiService para produtos novos. AffiliateLink e preenchido diretamente do offerLink
/// retornado pela API (diferente do ML, que depende de aprovacao do scoring — ver task-breakdown
/// da Issue #5). Produto sem video nem imagem e salvo mesmo assim (MediaUrl/MediaType nulos).
/// </summary>
public class ShopeeCollector : IPlatformCollector
{
    private const string GraphQlUrl = "https://open-api.affiliate.shopee.com.br/graphql";
    private const string Path = "/graphql";
    private const string DefaultCategory = "Geral";
    private const int RateLimitDelayMs = 1000;

    private static readonly int[] RetryDelaysMs = { 2000, 4000, 8000 };

    private readonly HttpClient _httpClient;
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly IAiService _aiService;
    private readonly ILogger<ShopeeCollector> _logger;

    public ShopeeCollector(
        HttpClient httpClient,
        AfiliadoBotDbContext dbContext,
        IAiService aiService,
        ILogger<ShopeeCollector> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _aiService = aiService;
        _logger = logger;
    }

    public Platform Platform => Platform.Shopee;

    public async Task<IEnumerable<Product>> CollectAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);

        ValidateCredentials(settings);

        await Task.Delay(RateLimitDelayMs, ct);

        var response = await SendWithRetryAsync(settings, ct);

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

    private async Task<ShopeeSettings> LoadSettingsAsync(CancellationToken ct)
    {
        var keys = new[]
        {
            "shopee.app_id",
            "shopee.secret",
            "shopee.affiliate_id"
        };

        var values = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        values.TryGetValue("shopee.app_id", out var appId);
        values.TryGetValue("shopee.secret", out var secret);
        values.TryGetValue("shopee.affiliate_id", out var affiliateId);

        return new ShopeeSettings(
            appId ?? string.Empty,
            secret ?? string.Empty,
            affiliateId ?? string.Empty);
    }

    private static void ValidateCredentials(ShopeeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AppId))
            throw new InvalidOperationException("Credencial ausente: shopee.app_id");

        if (string.IsNullOrWhiteSpace(settings.Secret))
            throw new InvalidOperationException("Credencial ausente: shopee.secret");

        if (string.IsNullOrWhiteSpace(settings.AffiliateId))
            throw new InvalidOperationException("Credencial ausente: shopee.affiliate_id");
    }

    private static string BuildGraphQlPayload()
    {
        var payload = new
        {
            query = "query { productOfferV2(sortType: 2, limit: 20) { nodes { productId productName priceMin originalPrice discount productImage offerLink } } }"
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task<string?> SendWithRetryAsync(ShopeeSettings settings, CancellationToken ct)
    {
        var payload = BuildGraphQlPayload();

        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            var request = BuildSignedRequest(settings, payload);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ShopeeCollector: falha de rede na busca de produtos. Ciclo abortado sem exception.");
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
                    "ShopeeCollector: rate limit (429) apos {Attempts} tentativas. Ciclo abortado sem exception.",
                    RetryDelaysMs.Length + 1);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ShopeeCollector: resposta HTTP {StatusCode} na busca de produtos. Ciclo abortado sem exception.",
                    (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }

        return null;
    }

    private HttpRequestMessage BuildSignedRequest(ShopeeSettings settings, string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ShopeeHmacSigner.Sign(settings.AppId, Path, timestamp, settings.Secret, payload);

        var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("Authorization", $"SHA256 {signature}");

        return request;
    }

    private static List<ShopeeItem> ParseItems(string responseBody)
    {
        var items = new List<ShopeeItem>();

        using var doc = JsonDocument.Parse(responseBody);

        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("productOfferV2", out var productOffer) ||
            !productOffer.TryGetProperty("nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var item in nodes.EnumerateArray())
        {
            var productId = item.TryGetProperty("productId", out var idProp)
                ? idProp.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(productId))
                continue;

            var productName = item.TryGetProperty("productName", out var nameProp)
                ? nameProp.GetString() ?? string.Empty
                : string.Empty;

            decimal priceMin = 0;
            if (item.TryGetProperty("priceMin", out var priceMinProp) &&
                priceMinProp.ValueKind != JsonValueKind.Null)
            {
                priceMin = priceMinProp.GetDecimal();
            }

            decimal? originalPrice = null;
            if (item.TryGetProperty("originalPrice", out var originalPriceProp) &&
                originalPriceProp.ValueKind != JsonValueKind.Null)
            {
                originalPrice = originalPriceProp.GetDecimal();
            }

            decimal? discount = null;
            if (item.TryGetProperty("discount", out var discountProp) &&
                discountProp.ValueKind != JsonValueKind.Null)
            {
                discount = discountProp.GetDecimal();
            }

            string? productVideo = null;
            if (item.TryGetProperty("productVideo", out var videoProp) &&
                videoProp.ValueKind != JsonValueKind.Null)
            {
                productVideo = videoProp.GetString();
            }

            string? productImage = null;
            if (item.TryGetProperty("productImage", out var imageProp) &&
                imageProp.ValueKind != JsonValueKind.Null)
            {
                productImage = imageProp.GetString();
            }

            var offerLink = item.TryGetProperty("offerLink", out var offerLinkProp)
                ? offerLinkProp.GetString()
                : null;

            var finalOriginalPrice = originalPrice ?? priceMin;
            var discountPct = discount ?? (finalOriginalPrice > 0
                ? Math.Round((1 - (priceMin / finalOriginalPrice)) * 100, 2)
                : 0);

            if (discountPct < 0) discountPct = 0;
            if (discountPct > 100) discountPct = 100;

            string? mediaUrl = null;
            string? mediaType = null;

            if (!string.IsNullOrWhiteSpace(productVideo))
            {
                mediaUrl = productVideo;
                mediaType = "video";
            }
            else if (!string.IsNullOrWhiteSpace(productImage))
            {
                mediaUrl = productImage;
                mediaType = "image";
            }

            items.Add(new ShopeeItem(
                productId!,
                productName,
                priceMin,
                finalOriginalPrice,
                discountPct,
                mediaUrl,
                mediaType,
                offerLink));
        }

        return items;
    }

    private async Task<Product?> UpsertProductAsync(ShopeeItem item, CancellationToken ct)
    {
        var existing = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Platform == Platform.Shopee && p.ExternalId == item.ProductId, ct);

        if (existing is not null)
        {
            existing.UpdateFromCollector(
                item.PriceMin,
                item.OriginalPrice,
                item.DiscountPct,
                imageUrl: null,
                mediaUrl: item.MediaUrl,
                mediaType: item.MediaType);
            return existing;
        }

        var slug = GenerateSlug(item.ProductName, item.ProductId);

        var product = new Product(
            title: item.ProductName,
            description: item.ProductName,
            salePrice: item.PriceMin,
            originalPrice: item.OriginalPrice,
            discountPct: item.DiscountPct,
            affiliateLink: item.OfferLink,
            slug: slug,
            category: DefaultCategory,
            platform: Platform.Shopee,
            externalId: item.ProductId,
            mediaUrl: item.MediaUrl,
            mediaType: item.MediaType);

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

    private record ShopeeSettings(
        string AppId,
        string Secret,
        string AffiliateId);

    private record ShopeeItem(
        string ProductId,
        string ProductName,
        decimal PriceMin,
        decimal OriginalPrice,
        decimal DiscountPct,
        string? MediaUrl,
        string? MediaType,
        string? OfferLink);
}
