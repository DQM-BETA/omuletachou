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

public class AmazonCollectorTests
{
    private const string ValidJsonResponse = """
        {
          "SearchResult": {
            "Items": [
              {
                "ASIN": "B000TESTE1",
                "ItemInfo": { "Title": { "DisplayValue": "Fone Bluetooth XPTO" } },
                "Offers": {
                  "Listings": [
                    { "Price": { "Amount": 99.90 }, "SavingBasis": { "Amount": 199.90 } }
                  ]
                },
                "Images": { "Primary": { "Large": { "URL": "https://img.amazon.com/fone.jpg" } } }
              }
            ]
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

    private static async Task SeedSettingsAsync(
        AfiliadoBotDbContext db,
        string accessKey = "AKIATESTE",
        string secretKey = "secretTeste",
        string partnerTag = "meutag-20",
        string marketplace = "www.amazon.com.br",
        string maxResults = "20")
    {
        db.AppSettings.AddRange(
            new AppSetting("amazon.access_key", accessKey),
            new AppSetting("amazon.secret_key", secretKey),
            new AppSetting("amazon.partner_tag", partnerTag),
            new AppSetting("amazon.marketplace", marketplace),
            new AppSetting("amazon.max_results", maxResults));

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

    private static HttpClient CreateSequenceHttpClient(params HttpResponseMessage[] responses)
    {
        var queue = new Queue<HttpResponseMessage>(responses);
        return CreateHttpClient(_ => queue.Count > 1 ? queue.Dequeue() : queue.Peek());
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
        // Arrange
        using var db = CreateInMemoryContext();
        await SeedSettingsAsync(db);
        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidJsonResponse));

        var collector = new AmazonCollector(httpClient, db, aiMock.Object, NullLogger<AmazonCollector>.Instance);

        // Act
        var result = (await collector.CollectAsync()).ToList();

        // Assert
        result.Should().HaveCount(1);
        var product = result[0];
        product.SalePrice.Should().Be(99.90m);
        product.AffiliateLink.Should().Be("https://www.amazon.com.br/dp/B000TESTE1?tag=meutag-20");
        product.ExternalId.Should().Be("B000TESTE1");
    }

    [Fact]
    public async Task CollectAsync_LancaException_QuandoCredenciaisAusentes()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        await SeedSettingsAsync(db, accessKey: "");
        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(_ => throw new InvalidOperationException("Nao deveria chamar HTTP"));

        var collector = new AmazonCollector(httpClient, db, aiMock.Object, NullLogger<AmazonCollector>.Instance);

        // Act
        var act = async () => await collector.CollectAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CollectAsync_LancaException_QuandoMarketplaceInvalido()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        await SeedSettingsAsync(db, marketplace: "www.amazon.com");
        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(_ => throw new InvalidOperationException("Nao deveria chamar HTTP"));

        var collector = new AmazonCollector(httpClient, db, aiMock.Object, NullLogger<AmazonCollector>.Instance);

        // Act
        var act = async () => await collector.CollectAsync();

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Marketplace não suportado nesta versão");
    }

    [Fact]
    public async Task CollectAsync_FazUpsert_QuandoProdutoJaExiste()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        await SeedSettingsAsync(db);

        var existingProduct = new Product(
            title: "Fone Bluetooth XPTO",
            description: "desc",
            salePrice: 150m,
            originalPrice: 200m,
            discountPct: 25m,
            affiliateLink: "https://www.amazon.com.br/dp/B000TESTE1?tag=meutag-20",
            slug: "fone-bluetooth-xpto-b000teste1",
            category: "Geral",
            platform: Platform.Amazon,
            externalId: "B000TESTE1");
        existingProduct.UpdateAiResult(9, "Otimo", "caption antiga");
        existingProduct.MarkAsPublished();

        db.Products.Add(existingProduct);
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidJsonResponse));

        var collector = new AmazonCollector(httpClient, db, aiMock.Object, NullLogger<AmazonCollector>.Instance);

        // Act
        var result = (await collector.CollectAsync()).ToList();

        // Assert
        result.Should().HaveCount(1);

        var totalWithSameKey = await db.Products.CountAsync(p => p.Platform == Platform.Amazon && p.ExternalId == "B000TESTE1");
        totalWithSameKey.Should().Be(1);

        var updated = await db.Products.FirstAsync(p => p.ExternalId == "B000TESTE1");
        updated.Id.Should().Be(existingProduct.Id);
        updated.SalePrice.Should().Be(99.90m);
        updated.Status.Should().Be(ProductStatus.Published); // preservado
        updated.AiScore.Should().Be(9); // preservado, nao re-scoreado

        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CollectAsync_ChamaScoreProductAsync_QuandoProdutoNovo()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        await SeedSettingsAsync(db);
        var aiMock = CreateAiServiceMock(score: 8, reason: "Otimo desconto");
        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.OK, ValidJsonResponse));

        var collector = new AmazonCollector(httpClient, db, aiMock.Object, NullLogger<AmazonCollector>.Instance);

        // Act
        var result = (await collector.CollectAsync()).ToList();

        // Assert
        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        result[0].AiScore.Should().Be(8);
        result[0].Status.Should().Be(ProductStatus.Queued);
    }

    [Fact]
    public async Task CollectAsync_RetryComBackoff_QuandoRecebe429()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        await SeedSettingsAsync(db);
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateSequenceHttpClient(
            JsonResponse(HttpStatusCode.TooManyRequests, string.Empty),
            JsonResponse(HttpStatusCode.OK, ValidJsonResponse));

        var collector = new AmazonCollector(httpClient, db, aiMock.Object, NullLogger<AmazonCollector>.Instance);

        // Act
        var result = (await collector.CollectAsync()).ToList();

        // Assert — retry ocorreu (2s de espera) e a segunda tentativa teve sucesso
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task CollectAsync_AbortaCicloSemException_QuandoTodasTentativasFalham()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        await SeedSettingsAsync(db);
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.TooManyRequests, string.Empty));

        var collector = new AmazonCollector(httpClient, db, aiMock.Object, NullLogger<AmazonCollector>.Instance);

        // Act
        IEnumerable<Product>? result = null;
        var act = async () => result = await collector.CollectAsync();

        // Assert
        await act.Should().NotThrowAsync();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }
}
