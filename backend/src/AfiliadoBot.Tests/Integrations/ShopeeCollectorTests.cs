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

public class ShopeeCollectorTests
{
    private const string ValidGraphQlResponseComImagem = """
        {
          "data": {
            "productOfferV2": {
              "nodes": [
                {
                  "productId": "987654",
                  "productName": "Fone Bluetooth XPTO",
                  "priceMin": 79.90,
                  "originalPrice": 129.90,
                  "discount": 38.5,
                  "productImage": "https://cf.shopee.com.br/file/imagem.jpg",
                  "offerLink": "https://shope.ee/abc123"
                }
              ]
            }
          }
        }
        """;

    private const string ValidGraphQlResponseSemMidia = """
        {
          "data": {
            "productOfferV2": {
              "nodes": [
                {
                  "productId": "111222",
                  "productName": "Produto Sem Midia",
                  "priceMin": 49.90,
                  "originalPrice": 59.90,
                  "discount": 16.7,
                  "productImage": null,
                  "offerLink": "https://shope.ee/xyz789"
                }
              ]
            }
          }
        }
        """;

    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static async Task SeedCredentialsAsync(
        AfiliadoBotDbContext db,
        string appId = "SHOPEE_APP_ID",
        string secret = "SHOPEE_SECRET",
        string affiliateId = "SHOPEE_AFFILIATE_ID")
    {
        db.AppSettings.AddRange(
            new AppSetting("shopee.app_id", appId),
            new AppSetting("shopee.secret", secret),
            new AppSetting("shopee.affiliate_id", affiliateId));

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

    [Fact]
    public async Task CollectAsync_RetornaProdutos_QuandoRespostaValida()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidGraphQlResponseComImagem));

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].ExternalId.Should().Be("987654");
        result[0].Title.Should().Be("Fone Bluetooth XPTO");
        result[0].SalePrice.Should().Be(79.90m);
        result[0].MediaUrl.Should().Be("https://cf.shopee.com.br/file/imagem.jpg");
        result[0].MediaType.Should().Be("image");
    }

    [Fact]
    public async Task CollectAsync_LancaException_QuandoCredenciaisAusentes()
    {
        using var db = CreateInMemoryContext();
        // credenciais ausentes
        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(_ => throw new InvalidOperationException("Nao deveria chamar HTTP"));

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        var act = async () => await collector.CollectAsync();

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("shopee.app_id");
    }

    [Fact]
    public async Task CollectAsync_PreencheAffiliateLink_DiretoDoOfferLink()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidGraphQlResponseComImagem));

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].AffiliateLink.Should().Be("https://shope.ee/abc123");
    }

    [Fact]
    public async Task CollectAsync_SalvaProdutoSemMidia_QuandoSemImagemOuVideo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidGraphQlResponseSemMidia));

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].MediaUrl.Should().BeNull();
        result[0].MediaType.Should().BeNull();
        result[0].AffiliateLink.Should().Be("https://shope.ee/xyz789");
    }

    [Fact]
    public async Task CollectAsync_FazUpsert_QuandoProdutoJaExiste()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var existingProduct = new Product(
            title: "Fone Bluetooth XPTO",
            description: "desc",
            salePrice: 89.90m,
            originalPrice: 129.90m,
            discountPct: 30.8m,
            affiliateLink: "https://shope.ee/old-link",
            slug: "fone-bluetooth-xpto-987654",
            category: "Geral",
            platform: Platform.Shopee,
            externalId: "987654");
        existingProduct.UpdateAiResult(9, "Otimo", "caption antiga");
        existingProduct.MarkAsPublished();

        db.Products.Add(existingProduct);
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidGraphQlResponseComImagem));

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);

        var totalWithSameKey = await db.Products.CountAsync(p => p.Platform == Platform.Shopee && p.ExternalId == "987654");
        totalWithSameKey.Should().Be(1);

        var updated = await db.Products.FirstAsync(p => p.ExternalId == "987654");
        updated.Id.Should().Be(existingProduct.Id);
        updated.SalePrice.Should().Be(79.90m);
        updated.Status.Should().Be(ProductStatus.Published); // preservado
        updated.AiScore.Should().Be(9); // preservado, nao re-scoreado

        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CollectAsync_ChamaScoreProductAsync_QuandoProdutoNovo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        var aiMock = CreateAiServiceMock(score: 8, reason: "Otimo desconto");

        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidGraphQlResponseComImagem));

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        result[0].AiScore.Should().Be(8);
        result[0].Status.Should().Be(ProductStatus.Queued);
    }

    [Fact]
    public async Task CollectAsync_RetryComBackoff_QuandoRecebe429()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        var aiMock = CreateAiServiceMock();

        var queue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(HttpStatusCode.TooManyRequests, string.Empty),
            JsonResponse(HttpStatusCode.OK, ValidGraphQlResponseComImagem)
        });

        var httpClient = CreateHttpClient(_ => queue.Count > 1 ? queue.Dequeue() : queue.Peek());

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task CollectAsync_AbortaCicloSemException_QuandoTodasTentativasFalham()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.TooManyRequests, string.Empty));

        var collector = new ShopeeCollector(httpClient, db, aiMock.Object, NullLogger<ShopeeCollector>.Instance);

        IEnumerable<Product>? result = null;
        var act = async () => result = await collector.CollectAsync();

        await act.Should().NotThrowAsync();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }
}
