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
/// Collector da Amazon Product Advertising API v5 (PAAPI v5).
/// Autentica via AWS Signature V4 manual, busca produtos em oferta ("oferta do dia"),
/// faz upsert por (Platform, ExternalId) e aciona scoring automatico via IAiService
/// para produtos novos.
/// </summary>
public class AmazonCollector : IPlatformCollector
{
    private const string Host = "webservices.amazon.com.br";
    private const string Path = "/paapi5/searchitems";
    private const string Region = "us-east-1";
    private const string Service = "ProductAdvertisingAPI";
    private const string AmzTarget = "com.amazon.paapi5.v1.ProductAdvertisingAPIv1.SearchItems";
    private const string RequiredMarketplace = "www.amazon.com.br";
    private const string DefaultCategory = "Geral";

    private static readonly int[] RetryDelaysMs = { 2000, 4000, 8000 };

    private readonly HttpClient _httpClient;
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly IAiService _aiService;
    private readonly ILogger<AmazonCollector> _logger;

    public AmazonCollector(
        HttpClient httpClient,
        AfiliadoBotDbContext dbContext,
        IAiService aiService,
        ILogger<AmazonCollector> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _aiService = aiService;
        _logger = logger;
    }

    public Platform Platform => Platform.Amazon;

    public async Task<IEnumerable<Product>> CollectAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);

        ValidateSettings(settings);

        var payload = BuildSearchItemsPayload(settings.PartnerTag, settings.MaxResults);

        var response = await SendWithRetryAsync(settings, payload, ct);

        var collected = new List<Product>();

        if (response is null)
        {
            // Retry esgotado — ciclo abortado sem exception, retorna o que ja foi processado.
            return collected;
        }

        var items = ParseItems(response);

        foreach (var item in items)
        {
            var product = await UpsertProductAsync(item, settings.PartnerTag, ct);
            if (product is not null)
                collected.Add(product);
        }

        await _dbContext.SaveChangesAsync(ct);

        return collected;
    }

    private async Task<AmazonSettings> LoadSettingsAsync(CancellationToken ct)
    {
        var keys = new[]
        {
            "amazon.access_key",
            "amazon.secret_key",
            "amazon.partner_tag",
            "amazon.marketplace",
            "amazon.max_results"
        };

        var values = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        values.TryGetValue("amazon.access_key", out var accessKey);
        values.TryGetValue("amazon.secret_key", out var secretKey);
        values.TryGetValue("amazon.partner_tag", out var partnerTag);
        values.TryGetValue("amazon.marketplace", out var marketplace);
        values.TryGetValue("amazon.max_results", out var maxResultsRaw);

        var maxResults = int.TryParse(maxResultsRaw, out var parsed) && parsed > 0 ? parsed : 20;

        return new AmazonSettings(
            accessKey ?? string.Empty,
            secretKey ?? string.Empty,
            partnerTag ?? string.Empty,
            marketplace ?? string.Empty,
            maxResults);
    }

    private static void ValidateSettings(AmazonSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AccessKey) ||
            string.IsNullOrWhiteSpace(settings.SecretKey) ||
            string.IsNullOrWhiteSpace(settings.PartnerTag))
        {
            throw new InvalidOperationException(
                "Credenciais da Amazon (access_key, secret_key, partner_tag) ausentes ou invalidas.");
        }

        if (settings.Marketplace != RequiredMarketplace)
        {
            throw new InvalidOperationException("Marketplace não suportado nesta versão");
        }
    }

    private static string BuildSearchItemsPayload(string partnerTag, int maxResults)
    {
        var payload = new
        {
            Keywords = "oferta do dia",
            SortBy = "Relevance",
            ItemCount = maxResults,
            PartnerTag = partnerTag,
            PartnerType = "Associates",
            Marketplace = RequiredMarketplace,
            Resources = new[]
            {
                "ItemInfo.Title",
                "Offers.Listings.Price",
                "Offers.Listings.SavingBasis",
                "Images.Primary.Large"
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task<string?> SendWithRetryAsync(AmazonSettings settings, string payload, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            var request = BuildSignedRequest(settings, payload);

            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt < RetryDelaysMs.Length)
                {
                    await Task.Delay(RetryDelaysMs[attempt], ct);
                    continue;
                }

                _logger.LogWarning(
                    "AmazonCollector: rate limit (429) apos {Attempts} tentativas. Ciclo abortado sem exception.",
                    RetryDelaysMs.Length + 1);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AmazonCollector: resposta HTTP {StatusCode} da PAAPI. Ciclo abortado sem exception.",
                    (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }

        return null;
    }

    private HttpRequestMessage BuildSignedRequest(AmazonSettings settings, string payload)
    {
        var headers = AwsSignatureV4.SignRequest(
            settings.AccessKey,
            settings.SecretKey,
            Region,
            Service,
            Host,
            Path,
            payload,
            AmzTarget);

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Host}{Path}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("X-Amz-Date", headers["X-Amz-Date"]);
        request.Headers.TryAddWithoutValidation("X-Amz-Content-Sha256", headers["X-Amz-Content-Sha256"]);
        request.Headers.TryAddWithoutValidation("Authorization", headers["Authorization"]);
        request.Headers.TryAddWithoutValidation("X-Amz-Target", AmzTarget);
        request.Headers.TryAddWithoutValidation("Content-Encoding", "amz-1.0");

        return request;
    }

    private static List<AmazonItem> ParseItems(string responseBody)
    {
        var items = new List<AmazonItem>();

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("SearchResult", out var searchResult) ||
            !searchResult.TryGetProperty("Items", out var itemsElement))
        {
            return items;
        }

        foreach (var item in itemsElement.EnumerateArray())
        {
            var asin = item.GetProperty("ASIN").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(asin))
                continue;

            var title = item.TryGetProperty("ItemInfo", out var itemInfo) &&
                        itemInfo.TryGetProperty("Title", out var titleProp) &&
                        titleProp.TryGetProperty("DisplayValue", out var displayValue)
                ? displayValue.GetString() ?? string.Empty
                : string.Empty;

            decimal salePrice = 0;
            decimal? savingBasis = null;
            string? imageUrl = null;

            if (item.TryGetProperty("Offers", out var offers) &&
                offers.TryGetProperty("Listings", out var listings) &&
                listings.ValueKind == JsonValueKind.Array &&
                listings.GetArrayLength() > 0)
            {
                var listing = listings[0];

                if (listing.TryGetProperty("Price", out var priceProp) &&
                    priceProp.TryGetProperty("Amount", out var amountProp))
                {
                    salePrice = amountProp.GetDecimal();
                }

                if (listing.TryGetProperty("SavingBasis", out var savingBasisProp) &&
                    savingBasisProp.TryGetProperty("Amount", out var savingAmountProp))
                {
                    savingBasis = savingAmountProp.GetDecimal();
                }
            }

            if (item.TryGetProperty("Images", out var images) &&
                images.TryGetProperty("Primary", out var primary) &&
                primary.TryGetProperty("Large", out var large) &&
                large.TryGetProperty("URL", out var urlProp))
            {
                imageUrl = urlProp.GetString();
            }

            var originalPrice = savingBasis ?? salePrice;
            var discountPct = originalPrice > 0
                ? Math.Round((1 - (salePrice / originalPrice)) * 100, 2)
                : 0;

            if (discountPct < 0) discountPct = 0;
            if (discountPct > 100) discountPct = 100;

            items.Add(new AmazonItem(asin, title, salePrice, originalPrice, discountPct, imageUrl));
        }

        return items;
    }

    private async Task<Product?> UpsertProductAsync(AmazonItem item, string partnerTag, CancellationToken ct)
    {
        var affiliateLink = $"https://www.amazon.com.br/dp/{item.Asin}?tag={partnerTag}";

        var existing = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Platform == Platform.Amazon && p.ExternalId == item.Asin, ct);

        if (existing is not null)
        {
            existing.UpdateFromCollector(item.SalePrice, item.OriginalPrice, item.DiscountPct, item.ImageUrl);
            return existing;
        }

        var slug = GenerateSlug(item.Title, item.Asin);

        var product = new Product(
            title: item.Title,
            description: item.Title,
            salePrice: item.SalePrice,
            originalPrice: item.OriginalPrice,
            discountPct: item.DiscountPct,
            affiliateLink: affiliateLink,
            slug: slug,
            category: DefaultCategory,
            platform: Platform.Amazon,
            imageUrl: item.ImageUrl,
            externalId: item.Asin);

        _dbContext.Products.Add(product);

        var score = await _aiService.ScoreProductAsync(product, ct);
        product.UpdateAiResult(score.Score, score.Reason, string.Empty);

        return product;
    }

    private static string GenerateSlug(string title, string asin)
    {
        var baseSlug = title
            .ToLowerInvariant()
            .Trim();

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

        return $"{slugBase}-{asin.ToLowerInvariant()}";
    }

    private record AmazonSettings(
        string AccessKey,
        string SecretKey,
        string PartnerTag,
        string Marketplace,
        int MaxResults);

    private record AmazonItem(
        string Asin,
        string Title,
        decimal SalePrice,
        decimal OriginalPrice,
        decimal DiscountPct,
        string? ImageUrl);
}
