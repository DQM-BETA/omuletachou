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

public class MercadoLivreCollectorTests
{
    private const string ValidSearchResponse = """
        {
          "results": [
            {
              "id": "MLB123456",
              "title": "Smartphone XPTO",
              "price": 899.90,
              "original_price": 1299.90,
              "thumbnail": "https://http2.mlstatic.com/thumb.jpg"
            }
          ]
        }
        """;

    private const string TokenResponse = """
        {
          "access_token": "APP_USR-novo-token",
          "token_type": "bearer",
          "expires_in": 21600,
          "scope": "offline_access read write"
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
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(req =>
            req.RequestUri!.ToString().Contains("/search")
                ? JsonResponse(HttpStatusCode.OK, ValidSearchResponse)
                : JsonResponse(HttpStatusCode.OK, TokenResponse));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].ExternalId.Should().Be("MLB123456");
        result[0].SalePrice.Should().Be(899.90m);
        result[0].MediaUrl.Should().Be("https://http2.mlstatic.com/thumb.jpg");
        result[0].MediaType.Should().Be("image");
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
            return JsonResponse(HttpStatusCode.OK, ValidSearchResponse);
        });

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
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
            return JsonResponse(HttpStatusCode.OK, ValidSearchResponse);
        });

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        tokenCalls.Should().Be(0);
    }

    [Fact]
    public async Task CollectAsync_FazUpsert_QuandoProdutoJaExiste()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));

        var existingProduct = new Product(
            title: "Smartphone XPTO",
            description: "desc",
            salePrice: 999m,
            originalPrice: 1299.90m,
            discountPct: 23m,
            affiliateLink: null,
            slug: "smartphone-xpto-mlb123456",
            category: "Geral",
            platform: Platform.MercadoLivre,
            externalId: "MLB123456");
        existingProduct.UpdateAiResult(9, "Otimo", "caption antiga");
        existingProduct.MarkAsPublished();

        db.Products.Add(existingProduct);
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var httpClient = CreateHttpClient(req =>
            req.RequestUri!.ToString().Contains("/search")
                ? JsonResponse(HttpStatusCode.OK, ValidSearchResponse)
                : JsonResponse(HttpStatusCode.OK, TokenResponse));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);

        var totalWithSameKey = await db.Products.CountAsync(p => p.Platform == Platform.MercadoLivre && p.ExternalId == "MLB123456");
        totalWithSameKey.Should().Be(1);

        var updated = await db.Products.FirstAsync(p => p.ExternalId == "MLB123456");
        updated.Id.Should().Be(existingProduct.Id);
        updated.SalePrice.Should().Be(899.90m);
        updated.Status.Should().Be(ProductStatus.Published); // preservado
        updated.AiScore.Should().Be(9); // preservado, nao re-scoreado

        aiMock.Verify(a => a.ScoreProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CollectAsync_ChamaScoreProductAsync_QuandoProdutoNovo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock(score: 8, reason: "Otimo desconto");

        var httpClient = CreateHttpClient(req =>
            req.RequestUri!.ToString().Contains("/search")
                ? JsonResponse(HttpStatusCode.OK, ValidSearchResponse)
                : JsonResponse(HttpStatusCode.OK, TokenResponse));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

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
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var searchQueue = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(HttpStatusCode.TooManyRequests, string.Empty),
            JsonResponse(HttpStatusCode.OK, ValidSearchResponse)
        });

        var httpClient = CreateHttpClient(req =>
        {
            if (req.RequestUri!.ToString().Contains("/oauth/token"))
                return JsonResponse(HttpStatusCode.OK, TokenResponse);

            return searchQueue.Count > 1 ? searchQueue.Dequeue() : searchQueue.Peek();
        });

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task CollectAsync_AbortaCicloSemException_QuandoTodasTentativasFalham()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(req =>
            req.RequestUri!.ToString().Contains("/oauth/token")
                ? JsonResponse(HttpStatusCode.OK, TokenResponse)
                : JsonResponse(HttpStatusCode.TooManyRequests, string.Empty));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        IEnumerable<Product>? result = null;
        var act = async () => result = await collector.CollectAsync();

        await act.Should().NotThrowAsync();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectAsync_NaoPreencheAffiliateLink_ProdutoFicaNull()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        await SeedTokenAsync(db, "token-valido", DateTime.UtcNow.AddHours(1));
        var aiMock = CreateAiServiceMock();

        var httpClient = CreateHttpClient(req =>
            req.RequestUri!.ToString().Contains("/search")
                ? JsonResponse(HttpStatusCode.OK, ValidSearchResponse)
                : JsonResponse(HttpStatusCode.OK, TokenResponse));

        var collector = new MercadoLivreCollector(httpClient, db, aiMock.Object, NullLogger<MercadoLivreCollector>.Instance);

        var result = (await collector.CollectAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].AffiliateLink.Should().BeNull();
    }
}
