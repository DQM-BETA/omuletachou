using System.Net;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Integrations.Social;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AfiliadoBot.Tests.Integrations;

public class InstagramPublisherTests
{
    private const string PageId = "17841400000000000";
    private const string CreationId = "creation-123";

    private static readonly TimeSpan FastPollInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan FastPollTimeout = TimeSpan.FromMilliseconds(60);

    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static async Task SeedCredentialsAsync(
        AfiliadoBotDbContext db,
        string accessToken = "token123",
        string appId = "app-id",
        string appSecret = "app-secret",
        string pageId = PageId,
        DateTime? tokenExpiresAt = null)
    {
        db.AppSettings.Add(new AppSetting("instagram.access_token", accessToken));
        db.AppSettings.Add(new AppSetting("instagram.app_id", appId));
        db.AppSettings.Add(new AppSetting("instagram.app_secret", appSecret));
        db.AppSettings.Add(new AppSetting("instagram.page_id", pageId));
        db.AppSettings.Add(new AppSetting(
            "instagram.token_expires_at",
            (tokenExpiresAt ?? DateTime.UtcNow.AddDays(30)).ToString("O")));
        await db.SaveChangesAsync();
    }

    private static Product CriarProduto(
        string? mediaLocalPath,
        string? mediaUrl,
        string? mediaType,
        string? aiCaption = "Legenda gerada por IA")
    {
        var product = new Product(
            title: "Produto Teste",
            description: "Descricao",
            salePrice: 100m,
            originalPrice: 120m,
            discountPct: 10m,
            affiliateLink: "https://link.teste",
            slug: Guid.NewGuid().ToString(),
            category: "Geral",
            platform: Platform.Amazon,
            mediaUrl: mediaUrl,
            mediaType: mediaType);

        product.UpdateAiResult(8, "Bom desconto", aiCaption ?? string.Empty);
        product.SetLocalMedia(mediaLocalPath, mediaType);
        return product;
    }

    /// <summary>
    /// Mock de HttpMessageHandler que roteia por host/path: fb_exchange_token (renovacao),
    /// POST /{page-id}/media (etapa 1), GET /{creation-id}?fields=status_code (etapa 2, polling)
    /// e POST /{page-id}/media_publish (etapa 3). Captura todas as requisicoes para inspecao.
    /// </summary>
    private static (HttpClient Client, List<HttpRequestMessage> Requests) CreateHttpClient(
        bool renewFails = false,
        string renewedAccessToken = "renewed-token",
        bool createContainerFails = false,
        string creationId = CreationId,
        Queue<string>? pollStatusSequence = null,
        bool publishFails = false)
    {
        var requests = new List<HttpRequestMessage>();
        var handlerMock = new Mock<HttpMessageHandler>();
        var statusQueue = pollStatusSequence ?? new Queue<string>(new[] { "FINISHED" });

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
            {
                requests.Add(req);
                var url = req.RequestUri!.ToString();

                if (url.Contains("fb_exchange_token"))
                {
                    if (renewFails)
                        return new HttpResponseMessage(HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent("{\"error\":\"invalid_token\"}"),
                        };

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"access_token\":\"{renewedAccessToken}\",\"expires_in\":5184000}}"),
                    };
                }

                if (req.Method == HttpMethod.Post && url.Contains("/media_publish"))
                {
                    if (publishFails)
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        {
                            Content = new StringContent("{\"error\":\"publish failed\"}"),
                        };

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"id\":\"media-999\"}"),
                    };
                }

                if (req.Method == HttpMethod.Post && url.Contains("/media"))
                {
                    if (createContainerFails)
                        return new HttpResponseMessage(HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent("{\"error\":\"invalid video_url\"}"),
                        };

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"id\":\"{creationId}\"}}"),
                    };
                }

                if (req.Method == HttpMethod.Get && url.Contains("status_code"))
                {
                    var status = statusQueue.Count > 1 ? statusQueue.Dequeue() : statusQueue.Peek();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"status_code\":\"{status}\"}}"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        return (new HttpClient(handlerMock.Object), requests);
    }

    private static InstagramPublisher CreatePublisher(
        HttpClient httpClient, AfiliadoBotDbContext db, TimeSpan? pollInterval = null, TimeSpan? pollTimeout = null)
    {
        return new InstagramPublisher(
            httpClient, db, NullLogger<InstagramPublisher>.Instance,
            pollInterval ?? FastPollInterval, pollTimeout ?? FastPollTimeout);
    }

    // ---- CA1/CA2/CA3: fluxo feliz completo -------------------------------------------------

    [Fact]
    public async Task PublishAsync_ExecutaAs3Etapas_QuandoFluxoFeliz()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();

        var createRequest = requests.Single(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().EndsWith($"/{PageId}/media"));
        var createBody = await createRequest.Content!.ReadAsStringAsync();
        createBody.Should().Contain("media_type=REELS");
        createBody.Should().Contain(Uri.EscapeDataString("https://cdn.teste/video.mp4"));

        requests.Should().Contain(r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains($"/{CreationId}?fields=status_code"));

        var publishRequest = requests.Single(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().EndsWith("/media_publish"));
        var publishBody = await publishRequest.Content!.ReadAsStringAsync();
        publishBody.Should().Contain($"creation_id={CreationId}");
    }

    [Fact]
    public async Task PublishAsync_PollingContinuaAteFinished_QuandoInProgress()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var statusSequence = new Queue<string>(new[] { "IN_PROGRESS", "IN_PROGRESS", "FINISHED" });
        var (httpClient, requests) = CreateHttpClient(pollStatusSequence: statusSequence);
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        requests.Count(r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains("status_code")).Should().Be(3);
    }

    // ---- CA4: polling FAILED -----------------------------------------------------------------

    [Theory]
    [InlineData("FAILED")]
    [InlineData("ERROR")]
    [InlineData("EXPIRED")]
    public async Task PublishAsync_FalhaImediatamente_QuandoStatusFalho(string statusCode)
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var statusSequence = new Queue<string>(new[] { statusCode });
        var (httpClient, requests) = CreateHttpClient(pollStatusSequence: statusSequence);
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.CanRetry.Should().BeTrue();
        requests.Should().NotContain(r => r.RequestUri!.ToString().Contains("media_publish"));
    }

    // ---- CA5: timeout de polling ---------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_MarcaFailedComRetry_QuandoTimeoutDePolling()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        // Sempre IN_PROGRESS — nunca atinge FINISHED, forcando o timeout curto de teste.
        var statusSequence = new Queue<string>(new[] { "IN_PROGRESS", "IN_PROGRESS" });
        var (httpClient, _) = CreateHttpClient(pollStatusSequence: statusSequence);
        var publisher = CreatePublisher(httpClient, db, pollInterval: TimeSpan.FromMilliseconds(5), pollTimeout: TimeSpan.FromMilliseconds(30));

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.CanRetry.Should().BeTrue("timeout de polling permite retry, diferente do FailPermanently");
        item.RetryCount.Should().Be(1);
    }

    // ---- CA6/CA7/CA8: resolucao de midia ------------------------------------------------------

    [Fact]
    public async Task PublishAsync_MontaUrlPublica_ViaMediaLocalPathEBaseUrl()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        db.AppSettings.Add(new AppSetting("api.public_base_url", "https://api.omuletachou.com.br"));
        await db.SaveChangesAsync();

        var product = CriarProduto(mediaLocalPath: "/app/media/abc123.mp4", mediaUrl: null, mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        var createRequest = requests.Single(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().EndsWith($"/{PageId}/media"));
        var body = await createRequest.Content!.ReadAsStringAsync();
        body.Should().Contain(Uri.EscapeDataString("https://api.omuletachou.com.br/media/abc123.mp4"));
    }

    [Fact]
    public async Task PublishAsync_UsaMediaUrl_QuandoMediaLocalPathNulo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);
        db.AppSettings.Add(new AppSetting("api.public_base_url", "https://api.omuletachou.com.br"));
        await db.SaveChangesAsync();

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/original.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        var createRequest = requests.Single(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().EndsWith($"/{PageId}/media"));
        var body = await createRequest.Content!.ReadAsStringAsync();
        body.Should().Contain(Uri.EscapeDataString("https://cdn.teste/original.mp4"));
    }

    [Fact]
    public async Task PublishAsync_FalhaSemCriarContainer_QuandoNenhumaMidiaResolvivel()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        // MediaType = "video" mas nem MediaLocalPath nem MediaUrl preenchidos — edge case,
        // passa pelo fallback CA15 (HasVideoMedia = false) antes de chegar em CA8. Para exercitar
        // CA8 isoladamente, simulamos MediaLocalPath preenchido sem api.public_base_url e sem
        // MediaUrl de fallback.
        var product = CriarProduto(mediaLocalPath: "/app/media/orfao.mp4", mediaUrl: null, mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.CanRetry.Should().BeTrue();
        requests.Should().NotContain(r => r.RequestUri!.ToString().Contains($"/{PageId}/media") && r.Method == HttpMethod.Post);
    }

    // ---- CA9/CA10/CA11: disclosure -------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_AnexaDisclosure_QuandoLegendaNaoContem()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video", aiCaption: "Confira essa oferta incrivel!");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        await publisher.PublishAsync(item);

        var createRequest = requests.Single(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().EndsWith($"/{PageId}/media"));
        var body = await createRequest.Content!.ReadAsStringAsync();
        var decoded = System.Net.WebUtility.UrlDecode(body);
        decoded.Should().Contain("Confira essa oferta incrivel! #publi");
    }

    [Theory]
    [InlineData("Confira a oferta #publi")]
    [InlineData("Confira a oferta #publicidade")]
    [InlineData("Confira a oferta #Publi")]
    public async Task PublishAsync_NaoDuplicaDisclosure_QuandoJaPresente(string captionComDisclosure)
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video", aiCaption: captionComDisclosure);
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        await publisher.PublishAsync(item);

        var createRequest = requests.Single(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().EndsWith($"/{PageId}/media"));
        var body = await createRequest.Content!.ReadAsStringAsync();
        var decoded = System.Net.WebUtility.UrlDecode(body);

        var occurrences = System.Text.RegularExpressions.Regex.Matches(decoded, "#publi", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        occurrences.Should().Be(1);
    }

    // ---- CA12/CA13: renovacao de token ---------------------------------------------------------

    [Fact]
    public async Task PublishAsync_RenovaToken_QuandoRestamMenosDe7Dias()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, tokenExpiresAt: DateTime.UtcNow.AddDays(3));

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient(renewedAccessToken: "novo-token-abc");
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        requests.Should().Contain(r => r.RequestUri!.ToString().Contains("fb_exchange_token"));

        var createRequest = requests.Single(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().EndsWith($"/{PageId}/media"));
        var body = await createRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("access_token=novo-token-abc");

        var persistedToken = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "instagram.access_token");
        persistedToken!.Value.Should().Be("novo-token-abc");

        var persistedExpiry = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "instagram.token_expires_at");
        DateTime.Parse(persistedExpiry!.Value).Should().BeAfter(DateTime.UtcNow.AddDays(7));
    }

    [Fact]
    public async Task PublishAsync_NaoRenovaToken_QuandoValidadeSuficiente()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, tokenExpiresAt: DateTime.UtcNow.AddDays(30));

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        requests.Should().NotContain(r => r.RequestUri!.ToString().Contains("fb_exchange_token"));
    }

    // ---- CA14: falha na renovacao ---------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_FailPermanently_QuandoRenovacaoDeTokenFalha()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, tokenExpiresAt: DateTime.UtcNow.AddDays(1));

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _) = CreateHttpClient(renewFails: true);
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.CanRetry.Should().BeFalse();

        var flag = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "instagram.token_invalid");
        flag.Should().NotBeNull();
        flag!.Value.Should().Be("true");
    }

    // ---- CA15: fallback de seguranca sem video ---------------------------------------------------

    [Fact]
    public async Task PublishAsync_FailPermanently_QuandoProdutoSemVideo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: "image");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.ErrorMessage.Should().Be("Produto sem mídia de vídeo, não aplicável ao Instagram");
        item.CanRetry.Should().BeFalse();
        requests.Should().BeEmpty();
    }

    // ---- Credenciais ausentes / creation container / publish falhos ------------------------------

    [Fact]
    public async Task PublishAsync_LancaException_QuandoCredenciaisAusentes()
    {
        using var db = CreateInMemoryContext();
        // Sem seed de credenciais.

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var act = async () => await publisher.PublishAsync(item);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PublishAsync_FalhaComRetry_QuandoCriacaoDeContainerFalha()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _) = CreateHttpClient(createContainerFails: true);
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.CanRetry.Should().BeTrue();
        item.ErrorMessage.Should().Contain("container de midia");
    }

    [Fact]
    public async Task PublishAsync_FalhaComRetry_QuandoPublicacaoDoContainerFalha()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Instagram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _) = CreateHttpClient(publishFails: true);
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.CanRetry.Should().BeTrue();
    }
}
