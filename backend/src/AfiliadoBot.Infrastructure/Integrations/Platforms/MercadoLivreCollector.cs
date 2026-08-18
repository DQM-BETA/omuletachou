using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Domain.Services;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Integrations.Platforms;

/// <summary>
/// Collector do MercadoLivre. Autentica via OAuth2 client_credentials com cache/refresh
/// automatico de token em app_settings, coleta produtos por categoria via Highlights API
/// (Issue #182/#183 — <c>/sites/MLB/search</c> foi descontinuado pela plataforma, e o multi-get
/// <c>/items?ids=...</c> retorna HTTP 403 com as credenciais atuais), faz upsert por
/// (Platform, ExternalId) e aciona scoring automatico via IAiService para produtos novos.
/// AffiliateLink NAO e preenchido na coleta (fica null ate aprovacao pelo ProcessorJob, Issue #6).
/// </summary>
public class MercadoLivreCollector : IPlatformCollector
{
    private const string OAuthUrl = "https://api.mercadolibre.com/oauth/token";
    private const string ApiBaseUrl = "https://api.mercadolibre.com";
    private const string SourceUrlBase = "https://www.mercadolivre.com.br/p";

    // Delay defensivo entre chamadas HTTP consecutivas ao dominio api.mercadolibre.com (Issue
    // #182, especificacao-tecnica.md secao 2.4 / design.md secao 5.2 — sem rate limiter dedicado,
    // volume real (~168 chamadas/ciclo, 1x/dia) esta muitas ordens de grandeza abaixo da cota de
    // 18000 req/hora da aplicacao).
    private const int RequestDelayMs = 300;

    private static readonly TimeSpan TokenExpiryMargin = TimeSpan.FromMinutes(5);

    // Categoria interna -> ID(s) reais de categoria/subcategoria do Mercado Livre (site MLB).
    // Valores confirmados ao vivo pelo LT em 2026-08-17 (design.md secao 3.4) via
    // GET /sites/MLB/categories + GET /categories/{id}. Todas as 8 categorias mapeiam 1:1 (nenhum
    // caso N:1 confirmado necessario na arvore atual) — o array supporta N:1 sem mudanca de forma
    // caso isso mude no futuro.
    private static readonly Dictionary<string, string[]> CategoryMap = new()
    {
        ["Eletrodomésticos"] = new[] { "MLB5726" },   // "Eletrodomésticos" — categoria de topo, 1:1
        ["Climatização"] = new[] { "MLB252358" },     // "Ar e Ventilação" — subcategoria de Eletrodomésticos (MLB5726); Highlights aceita ID de subcategoria normalmente
        ["Ferramentas"] = new[] { "MLB263532" },      // "Ferramentas" — categoria de topo, 1:1
        ["Eletrônicos"] = new[] { "MLB1000" },        // "Eletrônicos, Áudio e Vídeo" — categoria de topo, 1:1
        ["Casa e Cozinha"] = new[] { "MLB1574" },     // "Casa, Móveis e Decoração" — categoria de topo (cobre subárvore "Cozinha", MLB1618); sem N:1 necessário
        ["Beleza"] = new[] { "MLB1246" },             // "Beleza e Cuidado Pessoal" — categoria de topo, 1:1
        ["Moda"] = new[] { "MLB1430" },               // "Calçados, Roupas e Bolsas" — já agrega os 3, categoria de topo, 1:1
        ["Brinquedos"] = new[] { "MLB1132" },         // "Brinquedos e Hobbies" — categoria de topo, 1:1
    };

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

    /// <summary>
    /// Coleta produtos por categoria via Highlights API (Issue #182/#183). Para cada uma das 8
    /// categorias internas (CategoryMap), busca ate 10 catalog_product_id ranqueados
    /// (GET /highlights/MLB/category/{id}) e resolve cada um individualmente
    /// (GET /products/{id} + GET /products/{id}/items — sem multi-get, bloqueado por 403).
    /// Isolamento de falha: categoria que falha e pulada (log warning, ciclo continua); produto
    /// que falha e pulado (log warning, categoria continua).
    /// </summary>
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

        var collected = new List<Product>();

        // Dedup dentro do mesmo ciclo: o mesmo catalog_product_id pode aparecer nos Highlights de
        // mais de uma categoria (ex.: "Climatização" e "Casa e Cozinha"). Resolver/upsertar uma
        // unica vez por ciclo garante upsert unico sem depender de uma consulta ao banco entre
        // adds nao salvos ainda (SaveChangesAsync so roda no fim do ciclo) e evita gastar chamadas
        // HTTP redundantes contra a mesma cota.
        var resolvedInCycle = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (categoriaInterna, mlCategoryIds) in CategoryMap)
        {
            foreach (var mlCategoryId in mlCategoryIds)
            {
                List<string> highlightIds;
                try
                {
                    using var highlightsDoc = await GetJsonAsync(
                        $"{ApiBaseUrl}/highlights/MLB/category/{mlCategoryId}", accessToken, ct);
                    highlightIds = ParseHighlightIds(highlightsDoc.RootElement);
                }
                catch (MercadoLivreApiException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "MercadoLivreCollector: categoria {Categoria}/{CategoryId} falhou ao buscar highlights, pulando.",
                        categoriaInterna, mlCategoryId);
                    continue;
                }

                await Task.Delay(RequestDelayMs, ct);

                foreach (var catalogProductId in highlightIds)
                {
                    if (!resolvedInCycle.Add(catalogProductId))
                        continue;

                    var product = await ResolveAndUpsertAsync(catalogProductId, accessToken, ct);
                    if (product is not null)
                        collected.Add(product);
                }
            }
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

    /// <summary>
    /// Resolve um catalog_product_id em Product e faz o upsert. Isolamento de falha por produto
    /// (Issue #182, especificacao-tecnica.md secao 2.1): qualquer falha de rede/HTTP nao-2xx, ou
    /// resposta sem nome/sem itens validos, resulta em log de warning + null (produto pulado,
    /// ciclo/categoria continua).
    /// </summary>
    private async Task<Product?> ResolveAndUpsertAsync(string catalogProductId, string accessToken, CancellationToken ct)
    {
        string? title;
        string? thumbnail;
        decimal? salePrice;

        try
        {
            using var productDoc = await GetJsonAsync($"{ApiBaseUrl}/products/{catalogProductId}", accessToken, ct);
            await Task.Delay(RequestDelayMs, ct);
            (title, thumbnail) = ParseProduct(productDoc.RootElement);

            using var itemsDoc = await GetJsonAsync($"{ApiBaseUrl}/products/{catalogProductId}/items", accessToken, ct);
            await Task.Delay(RequestDelayMs, ct);
            salePrice = ParseCheapestItemPrice(itemsDoc.RootElement);
        }
        catch (MercadoLivreApiException ex)
        {
            _logger.LogWarning(ex, "MercadoLivreCollector: produto {CatalogProductId} nao resolvido, pulando.", catalogProductId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(title) || salePrice is null)
        {
            _logger.LogWarning(
                "MercadoLivreCollector: produto {CatalogProductId} sem nome ou sem itens validos ({Items}), pulando.",
                catalogProductId, salePrice is null ? "vazio" : "nome ausente");
            return null;
        }

        // Permalink de /products/{id} vem sempre vazio (design.md secao 10.1) — SourceUrl
        // construido pelo padrao publico de URL de pagina de catalogo do Mercado Livre
        // (especificacao-tecnica.md secao 1).
        var sourceUrl = $"{SourceUrlBase}/{catalogProductId}";

        return await UpsertProductAsync(catalogProductId, title!, salePrice.Value, thumbnail, sourceUrl, ct);
    }

    private async Task<Product?> UpsertProductAsync(
        string externalId,
        string title,
        decimal salePrice,
        string? thumbnail,
        string sourceUrl,
        CancellationToken ct)
    {
        var existing = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Platform == Platform.MercadoLivre && p.ExternalId == externalId, ct);

        // Nenhum campo de preco original/desconto disponivel em /products/{id} nem em
        // /products/{id}/items (especificacao-tecnica.md secao 2.3) — fallback documentado, nao
        // um bug: OriginalPrice = SalePrice, DiscountPct = 0.
        const decimal discountPct = 0;
        var originalPrice = salePrice;
        var mediaType = thumbnail is not null ? "image" : null;

        if (existing is not null)
        {
            existing.UpdateFromCollector(
                salePrice,
                originalPrice,
                discountPct,
                imageUrl: null,
                mediaUrl: thumbnail,
                mediaType: mediaType,
                sourceUrl: sourceUrl);
            return existing;
        }

        var slug = GenerateSlug(title, externalId);
        var (category, subcategory) = CategoryDetector.Detect(title);

        var product = new Product(
            title: title,
            description: title,
            salePrice: salePrice,
            originalPrice: originalPrice,
            discountPct: discountPct,
            affiliateLink: null,
            slug: slug,
            category: category,
            platform: Platform.MercadoLivre,
            externalId: externalId,
            mediaUrl: thumbnail,
            mediaType: mediaType,
            sourceUrl: sourceUrl,
            subcategory: subcategory);

        _dbContext.Products.Add(product);

        var score = await _aiService.ScoreProductAsync(product, ct);
        product.UpdateAiResult(score.Score, score.Reason, string.Empty);

        return product;
    }

    /// <summary>
    /// Chamada HTTP GET autenticada que devolve o corpo parseado como JSON. Falha de
    /// rede/cancelamento, resposta HTTP nao-2xx ou corpo 200 com JSON malformado viram
    /// <see cref="MercadoLivreApiException"/> (Issue #190 — <see cref="JsonException"/> do parse
    /// tambem precisa cair no mesmo tipo, senao escapa do try/catch dos chamadores e quebra o
    /// isolamento de falha por categoria/produto), para o chamador decidir o isolamento de falha.
    /// </summary>
    private async Task<JsonDocument> GetJsonAsync(string url, string accessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new MercadoLivreApiException($"Falha de rede em {url}.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new MercadoLivreApiException($"Resposta HTTP {(int)response.StatusCode} em {url}.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new MercadoLivreApiException($"Corpo JSON malformado em {url}.", ex);
        }
    }

    /// <summary>
    /// Parseia a resposta de GET /highlights/MLB/category/{id} em uma lista ordenada de
    /// catalog_product_id. Tolerante a variacao de envelope (campo "content" — formato publicado
    /// da Highlights API — ou "results"/array direto como fallback defensivo, ja que a chamada ao
    /// vivo nao pode ser reconfirmada neste ambiente, ver PR). Entradas sem "id" sao ignoradas;
    /// quando presente, "type" diferente de "PRODUCT" e ignorado (ex.: banners promocionais);
    /// resultado ordenado por "position" quando o campo existir.
    /// </summary>
    private static List<string> ParseHighlightIds(JsonElement root)
    {
        JsonElement itemsArray;
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            itemsArray = content;
        else if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            itemsArray = results;
        else if (root.ValueKind == JsonValueKind.Array)
            itemsArray = root;
        else
            return new List<string>();

        var entries = new List<(string Id, int Position)>();
        var fallbackPosition = 0;

        foreach (var entry in itemsArray.EnumerateArray())
        {
            fallbackPosition++;
            var position = fallbackPosition;
            string? id = null;

            if (entry.ValueKind == JsonValueKind.String)
            {
                id = entry.GetString();
            }
            else if (entry.ValueKind == JsonValueKind.Object)
            {
                if (entry.TryGetProperty("type", out var typeProp) &&
                    typeProp.ValueKind == JsonValueKind.String &&
                    !string.Equals(typeProp.GetString(), "PRODUCT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    id = idProp.GetString();

                if (entry.TryGetProperty("position", out var posProp) && posProp.ValueKind == JsonValueKind.Number)
                    position = posProp.GetInt32();
            }

            if (!string.IsNullOrWhiteSpace(id))
                entries.Add((id!, position));
        }

        return entries.OrderBy(e => e.Position).Select(e => e.Id).ToList();
    }

    /// <summary>
    /// Extrai nome e a primeira imagem de GET /products/{catalog_product_id}. "permalink" e
    /// "buy_box_winner" existem no payload real mas nao sao usados (permalink sempre vazio,
    /// buy_box_winner sempre null — design.md secao 10.1).
    /// </summary>
    private static (string? Title, string? Thumbnail) ParseProduct(JsonElement root)
    {
        var title = root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
            ? nameProp.GetString()
            : null;

        string? thumbnail = null;
        if (root.TryGetProperty("pictures", out var picturesProp) && picturesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var picture in picturesProp.EnumerateArray())
            {
                if (picture.ValueKind == JsonValueKind.Object &&
                    picture.TryGetProperty("url", out var urlProp) &&
                    urlProp.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(urlProp.GetString()))
                {
                    thumbnail = urlProp.GetString();
                    break;
                }
            }
        }

        return (title, thumbnail);
    }

    /// <summary>
    /// Extrai o menor "price" entre os itens de GET /products/{catalog_product_id}/items.
    /// Criterio de escolha entre vendedores (especificacao-tecnica.md secao 2.2): sem
    /// "buy_box_winner" utilizavel (sempre null), o menor preco e o criterio mais defensavel para
    /// surfacear a melhor oferta disponivel do produto de catalogo. Tolerante ao envelope
    /// ("results" ou array direto), mesma cautela defensiva do parsing de highlights.
    /// </summary>
    private static decimal? ParseCheapestItemPrice(JsonElement root)
    {
        JsonElement itemsArray;
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            itemsArray = results;
        else if (root.ValueKind == JsonValueKind.Array)
            itemsArray = root;
        else
            return null;

        decimal? cheapest = null;

        foreach (var entry in itemsArray.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            if (!entry.TryGetProperty("price", out var priceProp) || priceProp.ValueKind != JsonValueKind.Number)
                continue;

            var price = priceProp.GetDecimal();
            if (cheapest is null || price < cheapest)
                cheapest = price;
        }

        return cheapest;
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

    /// <summary>
    /// Falha de rede ou resposta HTTP nao-2xx numa chamada a api.mercadolibre.com — usada
    /// internamente para acionar o isolamento de falha por categoria/produto (Issue #182).
    /// </summary>
    private sealed class MercadoLivreApiException : Exception
    {
        public MercadoLivreApiException(string message) : base(message)
        {
        }

        public MercadoLivreApiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
