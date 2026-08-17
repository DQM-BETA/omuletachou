using System.Net;
using AfiliadoBot.Domain.DTOs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Integrations.Platforms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AfiliadoBot.Tests.Integrations;

/// <summary>
/// Testes do fluxo reconstruido do MercadoLivreCollector (Issue #182/#183) — Highlights API por
/// categoria (GET /highlights/MLB/category/{id}) + resolucao de detalhes por produto
/// (GET /products/{id} + GET /products/{id}/items), sem multi-get (/items?ids=... bloqueado por
/// 403, ver design.md secao 10). Fixtures seguem os campos confirmados ao vivo pelo LT
/// (design.md secao 10.1): "name"/"pictures" em /products/{id} (permalink sempre vazio, ignorado),
/// "item_id"/"price" em /products/{id}/items.
/// </summary>
public class MercadoLivreCollectorTests
{
    // IDs reais do CategoryMap (design.md secao 3.4) usados nos testes para simular categorias
    // especificas sem depender de todas as 8.
    private const string EletrodomesticosCategoryId = "MLB5726";
    private const string ClimatizacaoCategoryId = "MLB252358";
    private const string CasaECozinhaCategoryId = "MLB1574";

    private const string EmptyHighlightsResponse = """{ "content": [] }""";

    private const string TokenResponse = """
        {
          "access_token": "APP_USR-novo-token",
          "token_type": "bearer",
          "expires_in": 21600,
          "scope": "offline_access read write"
        }
        """;

    private static string HighlightsWithOneProduct(string catalogProductId) => $$"""
        {
          "content": [
            { "id": "{{catalogProductId}}", "position": 1, "type": "PRODUCT" }
          ]
        }
        """;

    private static string HighlightsWithTwoProducts(string catalogProductId1, string catalogProductId2) => $$"""
        {
          "content": [
            { "id": "{{catalogProductId1}}", "position": 1, "type": "PRODUCT" },
            { "id": "{{catalogProductId2}}", "position": 2, "type": "PRODUCT" }
          ]
        }
        """;

    private static string ProductResponse(string id, string name, string thumbnailUrl) => $$"""
        {
          "id": "{{id}}",
          "name": "{{name}}",
          "permalink": "",
          "buy_box_winner": null,
          "pictures": [
            { "id": "pic1", "url": "{{thumbnailUrl}}" }
          ]
        }
        """;

    private static string ItemsResponse(params decimal[] prices)
    {
        var results = string.Join(",", prices.Select((p, i) =>
            $$"""{ "item_id": "MLB100000000{{i}}", "seller_id": {{i + 1}}, "price": {{p.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "category_id": "MLB5726", "shipping": {} }"""));

        return $$"""{ "results": [{{results}}] }""";
    }

    private const string EmptyItemsResponse = """{ "results": [] }""";

    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static async Task SeedCredentialsAsync(
        AfiliadoBotDbContext db,
        string clientId = "ML_CLIENT_ID",
        string clientSecret = "ML_CLIENT_SECRET")
    {
        db.AppSettings.AddRange(
            new AppSetting("mercadolivre.client_id", clientId),
            new AppSetting("mercadolivre.client_secret", clientSecret));

        await db.SaveChangesAsync();
    }

    private static async Task SeedTokenAsync(
        AfiliadoBotDbContext db,
        string accessToken,
        DateTime expiresAt)
    {
        db.AppSettings.AddRange(
            new AppSetting("mercadolivre.access_token", accessToken),
            new AppSetting("mercadolivre.token_expires_at", expiresAt.ToString("O")));

        await db.SaveChangesAsync();
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) => responder(req));

        return new HttpClient(handlerMock.Object);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body)
    };

    private static Mock<IAiService> CreateAiServiceMock(int score = 8, string reason = "Bom desconto")
    {
        var mock = new Mock<IAiService>();
        mock.Setup(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductScore(score, reason, score >= Product.AiScoreThreshold));
        return mock;
    }

    /// <summary>
    /// Monta um HttpClient que roteia por padrao de URL: /oauth/token -> TokenResponse; qualquer
    /// /highlights/MLB/category/{id} nao coberto por <paramref name="overrides"/> -> highlights
    /// vazio (categoria sem produtos, isolada das demais). <paramref name="overrides"/> mapeia um
    /// trecho da URL (ex.: categoria/produto especifico) para a resposta a devolver — o primeiro
    /// override cujo trecho aparece na URL vence.
    /// </summary>
    private static HttpClient CreateMercadoLivreClient(
        params (string UrlContains, HttpResponseMessage Response)[] overrides)
    {
        return CreateHttpClient(req =>
        {
            var url = req.RequestUri!.ToString();

            if (url.Contains("/oauth/token"))
                return JsonResponse(HttpStatusCode.OK, TokenResponse);

            foreach (var (urlContains, response) in overrides)
            {
                if (url.Contains(urlContains))
                    return response;
            }

            if (url.Contains("/highlights/"))
                return JsonResponse(HttpStatusCode.OK, EmptyHighlightsResponse);

            return JsonResponse(HttpStatusCode.NotFound, "{}");
        });
    }

    [Fact]
    public async Task CollectAsync_ResolveEUpsertaProduto_QuandoHighlightsEProdutoResolvemOk()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateMercadoLivreClient(
            ($"/highlights/MLB/category/{EletrodomesticosCategoryId}",
                JsonResponse(HttpStatusCode.OK, HighlightsWithOneProduct("MLB16855791"))),
            ("/products/MLB16855791/items",
                JsonResponse(HttpStatusCode.OK, ItemsResponse(2599.90m, 2499.90m))),
            ("/products/MLB16855791",
                JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB16855791", "Geladeira Frost Free XPTO", "https://http2.mlstatic.com/geladeira.jpg"))));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].ExternalId.Should().Be("MLB16855791");
        result[0].Title.Should().Be("Geladeira Frost Free XPTO");
        result[0].SalePrice.Should().Be(2499.90m); // menor price entre os 2 itens retornados
        result[0].OriginalPrice.Should().Be(2499.90m); // fallback: sem sinal de desconto disponivel
        result[0].DiscountPct.Should().Be(0);
        result[0].MediaUrl.Should().Be("https://http2.mlstatic.com/geladeira.jpg");
        result[0].MediaType.Should().Be("image");
        result[0].SourceUrl.Should().Be("https://www.mercadolivre.com.br/p/MLB16855791");
        result[0].Category.Should().Be("Eletrodomésticos");
        result[0].Subcategory.Should().Be("Refrigeração");
    }

    [Fact]
    public async Task CollectAsync_LancaException_QuandoCredenciaisAusentes()
    {
        using var db = CreateInMemoryContext();
        // client_id ausente
        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(_ => throw new InvalidOperationException("Nao deveria chamar HTTP"));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var act = async () => await collector.CollectAsync();

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("mercadolivre.client_id");
    }

    [Fact]
    public async Task CollectAsync_RenovaToken_QuandoExpirado()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-expirado", DateTime.UtcNow.AddMinutes(-10));
        var aiMock = CreateAiServiceMock();

        var tokenCalls = 0;
        var httpClient = CreateHttpClient(req =>
        {
            if (req.RequestUri!.ToString().Contains("/oauth/token"))
            {
                tokenCalls++;
                return JsonResponse(HttpStatusCode.OK, TokenResponse);
            }
            return JsonResponse(HttpStatusCode.OK, EmptyHighlightsResponse);
        });

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().BeEmpty(); // nenhuma categoria retornou produtos
        tokenCalls.Should().Be(1);

        var savedToken = await db.AppSettings.FirstAsync(s => s.Key == "mercadolivre.access_token");
        savedToken.Value.Should().Be("APP_USR-novo-token");
    }

    [Fact]
    public async Task CollectAsync_ReusaToken_QuandoAindaValido()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var tokenCalls = 0;
        var httpClient = CreateHttpClient(req =>
        {
            if (req.RequestUri!.ToString().Contains("/oauth/token"))
            {
                tokenCalls++;
                return JsonResponse(HttpStatusCode.OK, TokenResponse);
            }
            return JsonResponse(HttpStatusCode.OK, EmptyHighlightsResponse);
        });

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        await collector.CollectAsync();

        tokenCalls.Should().Be(0);
    }

    [Fact]
    public async Task CollectAsync_PulaCategoria_QuandoHighlightsFalha_DemaisCategoriasContinuam()
    {
        // CA 5.1: categoria com falha (Highlights) e pulada, sem abortar o ciclo — as demais 7
        // categorias continuam sendo processadas normalmente.
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateMercadoLivreClient(
            ($"/highlights/MLB/category/{EletrodomesticosCategoryId}",
                JsonResponse(HttpStatusCode.InternalServerError, string.Empty)),
            ($"/highlights/MLB/category/{ClimatizacaoCategoryId}",
                JsonResponse(HttpStatusCode.OK, HighlightsWithOneProduct("MLB20000001"))),
            ("/products/MLB20000001/items",
                JsonResponse(HttpStatusCode.OK, ItemsResponse(199.90m))),
            ("/products/MLB20000001",
                JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB20000001", "Ventilador de Mesa XPTO", "https://http2.mlstatic.com/ventilador.jpg"))));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        IEnumerable<Product>? result = null;
        var act = async () => result = await collector.CollectAsync();

        await act.Should().NotThrowAsync();
        result.Should().NotBeNull();
        result!.Should().ContainSingle(p => p.ExternalId == "MLB20000001");
    }

    [Fact]
    public async Task CollectAsync_PulaProduto_QuandoResolucaoFalha_DemaisProdutosContinuam()
    {
        // CA 5.2/5.3: produto individual que falha ao resolver e pulado, sem abortar a categoria —
        // os demais produtos dos Highlights daquela categoria continuam sendo processados.
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateMercadoLivreClient(
            ($"/highlights/MLB/category/{EletrodomesticosCategoryId}",
                JsonResponse(HttpStatusCode.OK, HighlightsWithTwoProducts("MLB11111111", "MLB22222222"))),
            ("/products/MLB11111111", JsonResponse(HttpStatusCode.NotFound, """{"error":"not_found"}""")),
            ("/products/MLB22222222/items",
                JsonResponse(HttpStatusCode.OK, ItemsResponse(349.90m))),
            ("/products/MLB22222222",
                JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB22222222", "Micro-ondas XPTO", "https://http2.mlstatic.com/microondas.jpg"))));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().ContainSingle();
        result[0].ExternalId.Should().Be("MLB22222222");
    }

    [Fact]
    public async Task CollectAsync_PulaProduto_QuandoItemsVazio()
    {
        // CA 3.3/5.2: /products/{id}/items sem resultados — produto pulado (nao ha preco).
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateMercadoLivreClient(
            ($"/highlights/MLB/category/{EletrodomesticosCategoryId}",
                JsonResponse(HttpStatusCode.OK, HighlightsWithOneProduct("MLB33333333"))),
            ("/products/MLB33333333/items", JsonResponse(HttpStatusCode.OK, EmptyItemsResponse)),
            ("/products/MLB33333333",
                JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB33333333", "Fogão XPTO", "https://http2.mlstatic.com/fogao.jpg"))));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectAsync_FazUpsert_QuandoProdutoJaExiste()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));

        var existingProduct = new Product(
            title: "Geladeira Frost Free XPTO",
            description: "desc",
            salePrice: 2199m,
            originalPrice: 2199m,
            discountPct: 0m,
            affiliateLink: null,
            slug: "geladeira-frost-free-xpto-mlb16855791",
            category: "Geral",
            platform: Platform.MercadoLivre,
            externalId: "MLB16855791");
        existingProduct.UpdateAiResult(9, "Otimo", "caption antiga");
        existingProduct.MarkAsPublished();

        db.Products.Add(existingProduct);
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var httpClient = CreateMercadoLivreClient(
            ($"/highlights/MLB/category/{EletrodomesticosCategoryId}",
                JsonResponse(HttpStatusCode.OK, HighlightsWithOneProduct("MLB16855791"))),
            ("/products/MLB16855791/items",
                JsonResponse(HttpStatusCode.OK, ItemsResponse(2499.90m))),
            ("/products/MLB16855791",
                JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB16855791", "Geladeira Frost Free XPTO", "https://http2.mlstatic.com/geladeira.jpg"))));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);

        var totalWithSameKey = await db.Products.CountAsync(p => p.Platform == Platform.MercadoLivre && p.ExternalId == "MLB16855791");
        totalWithSameKey.Should().Be(1);

        var updated = await db.Products.FirstAsync(p => p.ExternalId == "MLB16855791");
        updated.Id.Should().Be(existingProduct.Id);
        updated.SalePrice.Should().Be(2499.90m);
        updated.Status.Should().Be(ProductStatus.Published); // preservado
        updated.AiScore.Should().Be(9); // preservado, nao re-scoreado
        updated.SourceUrl.Should().Be("https://www.mercadolivre.com.br/p/MLB16855791");
        updated.Category.Should().Be("Geral"); // sem recategorizacao retroativa (Issue #167)

        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CollectAsync_ChamaScoreProductAsync_QuandoProdutoNovo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock(score: 8, reason: "Otimo desconto");

        var httpClient = CreateMercadoLivreClient(
            ($"/highlights/MLB/category/{EletrodomesticosCategoryId}",
                JsonResponse(HttpStatusCode.OK, HighlightsWithOneProduct("MLB16855791"))),
            ("/products/MLB16855791/items",
                JsonResponse(HttpStatusCode.OK, ItemsResponse(2499.90m))),
            ("/products/MLB16855791",
                JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB16855791", "Geladeira Frost Free XPTO", "https://http2.mlstatic.com/geladeira.jpg"))));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        result[0].AiScore.Should().Be(8);
        result[0].Status.Should().Be(ProductStatus.Queued);
    }

    [Fact]
    public async Task CollectAsync_NaoPreencheAffiliateLink_ProdutoFicaNull()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateMercadoLivreClient(
            ($"/highlights/MLB/category/{EletrodomesticosCategoryId}",
                JsonResponse(HttpStatusCode.OK, HighlightsWithOneProduct("MLB16855791"))),
            ("/products/MLB16855791/items",
                JsonResponse(HttpStatusCode.OK, ItemsResponse(2499.90m))),
            ("/products/MLB16855791",
                JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB16855791", "Geladeira Frost Free XPTO", "https://http2.mlstatic.com/geladeira.jpg"))));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].AffiliateLink.Should().BeNull();
    }

    [Fact]
    public async Task CollectAsync_UpsertUnico_QuandoMesmoProdutoAparecEmDuasCategoriasNoMesmoCiclo()
    {
        // O mesmo catalog_product_id pode aparecer nos Highlights de mais de uma categoria interna
        // (ex.: um produto de cozinha destacado tanto em "Eletrodomésticos" quanto em
        // "Casa e Cozinha"). O upsert deve acontecer uma unica vez no ciclo.
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var productCalls = 0;
        var httpClient = CreateHttpClient(req =>
        {
            var url = req.RequestUri!.ToString();

            if (url.Contains("/oauth/token"))
                return JsonResponse(HttpStatusCode.OK, TokenResponse);

            if (url.Contains($"/highlights/MLB/category/{EletrodomesticosCategoryId}") ||
                url.Contains($"/highlights/MLB/category/{CasaECozinhaCategoryId}"))
                return JsonResponse(HttpStatusCode.OK, HighlightsWithOneProduct("MLB44444444"));

            if (url.Contains("/highlights/"))
                return JsonResponse(HttpStatusCode.OK, EmptyHighlightsResponse);

            if (url.Contains("/products/MLB44444444/items"))
                return JsonResponse(HttpStatusCode.OK, ItemsResponse(159.90m));

            if (url.Contains("/products/MLB44444444"))
            {
                productCalls++;
                return JsonResponse(HttpStatusCode.OK, ProductResponse(
                    "MLB44444444", "Airfryer XPTO", "https://http2.mlstatic.com/airfryer.jpg"));
            }

            return JsonResponse(HttpStatusCode.NotFound, "{}");
        });

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().ContainSingle(p => p.ExternalId == "MLB44444444");
        productCalls.Should().Be(1); // resolvido uma unica vez, mesmo aparecendo em 2 categorias

        var totalInDb = await db.Products.CountAsync(p => p.ExternalId == "MLB44444444");
        totalInDb.Should().Be(1);

        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
