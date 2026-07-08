using System.Net;
using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AfiliadoBot.Tests.Jobs;

public class ProcessorJobTests
{
    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static Product CriarProduto(
        string title = "Produto Teste",
        Platform platform = Platform.Amazon,
        string? affiliateLink = "https://amzn.to/xyz",
        string slug = "produto-teste",
        string category = "Geral",
        string? mediaUrl = null,
        int aiScore = 8,
        string? sourceUrl = null)
    {
        var product = new Product(
            title: title,
            description: "Descricao",
            salePrice: 100m,
            originalPrice: 120m,
            discountPct: 10m,
            affiliateLink: affiliateLink,
            slug: slug,
            category: category,
            platform: platform,
            mediaUrl: mediaUrl,
            sourceUrl: sourceUrl);

        product.UpdateAiResult(aiScore, "Bom desconto", "");
        return product;
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

    private static HttpClient CreateAffiliateLinkClient(string link = "https://ml.link/aff") =>
        CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"url\": \"{link}\"}}")
        });

    private static HttpClient CreateFailingHttpClient() =>
        CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

    private static Mock<IMediaStorage> CreateMediaStorageMock(string? localPath = "/app/media/abc.jpg", string mediaType = "image")
    {
        var mock = new Mock<IMediaStorage>();
        mock.Setup(m => m.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((localPath, mediaType));
        return mock;
    }

    private static Mock<IAiService> CreateAiServiceMock()
    {
        var mock = new Mock<IAiService>();
        mock.Setup(a => a.GenerateCaptionAsync(It.IsAny<Product>(), It.IsAny<SocialNetwork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Legenda gerada");
        return mock;
    }

    private static async Task SeedNetworkAsync(
        AfiliadoBotDbContext db,
        string network,
        bool enabled,
        params (string Key, string Value)[] credentials)
    {
        db.AppSettings.Add(new AppSetting($"networks.{network}.enabled", enabled ? "true" : "false"));
        foreach (var (key, value) in credentials)
            db.AppSettings.Add(new AppSetting(key, value));

        await db.SaveChangesAsync();
    }

    private static ProcessorJob CreateJob(
        AfiliadoBotDbContext db,
        Mock<IMediaStorage>? mediaStorage = null,
        Mock<IAiService>? aiService = null,
        HttpClient? httpClient = null)
    {
        return new ProcessorJob(
            db,
            (mediaStorage ?? CreateMediaStorageMock()).Object,
            (aiService ?? CreateAiServiceMock()).Object,
            httpClient ?? CreateAffiliateLinkClient(),
            NullLogger<ProcessorJob>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_MarcaProcessing_AoIniciarProcessamento()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: null);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Published);
    }

    [Fact]
    public async Task ExecuteAsync_BaixaMidia_QuandoMediaUrlPresente()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: "https://cdn.com/foto.jpg");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var mediaMock = CreateMediaStorageMock("/app/media/xyz.jpg", "image");
        var job = CreateJob(db, mediaStorage: mediaMock);
        await job.ExecuteAsync();

        mediaMock.Verify(m => m.DownloadAsync("https://cdn.com/foto.jpg", It.IsAny<CancellationToken>()), Times.Once);

        var reloaded = await db.Products.FirstAsync();
        reloaded.MediaLocalPath.Should().Be("/app/media/xyz.jpg");
    }

    [Fact]
    public async Task ExecuteAsync_NaoBaixaMidia_QuandoMediaUrlNulo()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: null);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var mediaMock = CreateMediaStorageMock();
        var job = CreateJob(db, mediaStorage: mediaMock);
        await job.ExecuteAsync();

        mediaMock.Verify(m => m.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GeraSlug_QuandoSlugVazio()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(slug: "", title: "Fone Bluetooth Legal");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Slug.Should().Be($"fone-bluetooth-legal-{reloaded.Id.ToString()[..6]}");
    }

    [Fact]
    public async Task ExecuteAsync_PulaSlug_QuandoJaPreenchido()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(slug: "slug-original");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Slug.Should().Be("slug-original");
    }

    [Fact]
    public async Task ExecuteAsync_DetectaCategoria_QuandoAindaGeral()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(title: "Fone de Ouvido Bluetooth", category: "Geral");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Category.Should().Be("Eletrônicos");
    }

    [Fact]
    public async Task ExecuteAsync_CriaPublicationQueue_ParaCadaRedeHabilitadaComCredenciais()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Telegram);
    }

    [Fact]
    public async Task ExecuteAsync_PulaRede_QuandoSemCredenciais()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await SeedNetworkAsync(db, "instagram", true); // sem credenciais
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FacebookRecebeManualPending_DemaisRedesScheduled()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await SeedNetworkAsync(db, "facebook", true, ("facebook.access_token", "tok"), ("facebook.page_id", "1"));
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().HaveCount(2);
        entries.Single(q => q.SocialNetwork == SocialNetwork.Facebook).Status.Should().Be(PublicationStatus.ManualPending);
        entries.Single(q => q.SocialNetwork == SocialNetwork.Telegram).Status.Should().Be(PublicationStatus.Scheduled);
    }

    [Fact]
    public async Task ExecuteAsync_DistribuiRoundRobin_PorScoreDesc()
    {
        using var db = CreateInMemoryContext();
        var produtos = new List<Product>();
        for (var i = 0; i < 6; i++)
        {
            var p = CriarProduto(title: $"Produto {i}", aiScore: 6 + i, slug: $"produto-{i}");
            produtos.Add(p);
        }

        db.Products.AddRange(produtos);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues
            .Include(q => q.Product)
            .Where(q => q.SocialNetwork == SocialNetwork.Telegram)
            .ToListAsync();

        entries.Should().HaveCount(6);

        // Maior score (produto 5, score 11) deve ir para o primeiro slot (9h de hoje).
        var orderedByScoreDesc = entries.OrderByDescending(q => q.Product!.AiScore).ToList();
        var today = DateTime.UtcNow.Date;

        orderedByScoreDesc[0].ScheduledAt.Date.Should().Be(today);
        orderedByScoreDesc[0].ScheduledAt.Hour.Should().Be(9);

        orderedByScoreDesc[1].ScheduledAt.Hour.Should().Be(12);
        orderedByScoreDesc[4].ScheduledAt.Date.Should().Be(today);
        orderedByScoreDesc[4].ScheduledAt.Hour.Should().Be(20);
        orderedByScoreDesc[5].ScheduledAt.Date.Should().Be(today.AddDays(1));
        orderedByScoreDesc[5].ScheduledAt.Hour.Should().Be(9);
    }

    [Fact]
    public async Task ExecuteAsync_MarcaPublished_AoFinalizarComSucesso()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Published);
    }

    [Fact]
    public async Task ExecuteAsync_MarcaError_QuandoFalhaGeracaoLinkML()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(
            platform: Platform.MercadoLivre,
            affiliateLink: null,
            sourceUrl: "https://produto.mercadolivre.com.br/MLB-123-produto-teste");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db, httpClient: CreateFailingHttpClient());
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Error);
        reloaded.AiReason.Should().Contain("link de afiliado");

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MarcaError_QuandoSourceUrlAusente()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(platform: Platform.MercadoLivre, affiliateLink: null, sourceUrl: null);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var callCount = 0;
        var httpClient = CreateHttpClient(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"url\": \"x\"}") };
        });

        var job = CreateJob(db, httpClient: httpClient);
        await job.ExecuteAsync();

        callCount.Should().Be(0, "nao deve chamar a API de afiliados com payload invalido quando SourceUrl esta ausente");

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Error);
        reloaded.AiReason.Should().Contain("SourceUrl ausente");

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_NaoProcessaProdutosRejected()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        product.UpdateAiResult(2, "Score baixo", ""); // Rejected
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Rejected);
    }

    [Fact]
    public async Task ExecuteAsync_NaoCriaEntradaYoutube_QuandoProdutoSemVideo()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: null);
        db.Products.Add(product);
        await SeedNetworkAsync(db, "youtube", true,
            ("youtube.client_id", "cid"), ("youtube.client_secret", "csecret"), ("youtube.refresh_token", "rtoken"));
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var job = CreateJob(db, aiService: aiMock);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().NotContain(q => q.SocialNetwork == SocialNetwork.Youtube);
        aiMock.Verify(a => a.GenerateCaptionAsync(It.IsAny<Product>(), SocialNetwork.Youtube, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CriaEntradaYoutube_QuandoProdutoComVideo()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: "https://cdn.com/video.mp4");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "youtube", true,
            ("youtube.client_id", "cid"), ("youtube.client_secret", "csecret"), ("youtube.refresh_token", "rtoken"));
        await db.SaveChangesAsync();

        var mediaMock = CreateMediaStorageMock("/app/media/video.mp4", "video");
        var aiMock = CreateAiServiceMock();
        var job = CreateJob(db, mediaStorage: mediaMock, aiService: aiMock);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Youtube);
        entries.Single(q => q.SocialNetwork == SocialNetwork.Youtube).ScheduledAt.Hour.Should().Be(9);
        aiMock.Verify(a => a.GenerateCaptionAsync(It.IsAny<Product>(), SocialNetwork.Youtube, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NaoAfetaDemaisRedes_QuandoYoutubeFiltrado()
    {
        // CA19 (regressao, nao-negociavel): produto sem video, elegivel para Telegram/Instagram/
        // TikTok/Facebook (com credenciais) — o filtro adicional do Youtube nao pode afetar as
        // demais redes, que devem se comportar exatamente como antes da correcao.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: null); // sem video
        db.Products.Add(product);
        await SeedNetworkAsync(db, "youtube", true,
            ("youtube.client_id", "cid"), ("youtube.client_secret", "csecret"), ("youtube.refresh_token", "rtoken"));
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await SeedNetworkAsync(db, "instagram", true, ("instagram.access_token", "tok"), ("instagram.page_id", "1"));
        await SeedNetworkAsync(db, "tiktok", true, ("tiktok.access_token", "tok"));
        await SeedNetworkAsync(db, "facebook", true, ("facebook.access_token", "tok"), ("facebook.page_id", "1"));
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();

        entries.Should().NotContain(q => q.SocialNetwork == SocialNetwork.Youtube);
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Telegram);
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Instagram);
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.TikTok);
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Facebook);
        entries.Single(q => q.SocialNetwork == SocialNetwork.Facebook).Status.Should().Be(PublicationStatus.ManualPending);
        entries.Should().HaveCount(4);
    }

    [Fact]
    public async Task ExecuteAsync_VerificaCredenciaisYoutubeCorretas()
    {
        // NetworkSettings.CredentialKeys da linha Youtube deve exigir client_id/client_secret/
        // refresh_token — access_token isolado (chave antiga) nao deve mais habilitar a rede.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: "https://cdn.com/video.mp4");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "youtube", true, ("youtube.access_token", "old-token-only"));
        await db.SaveChangesAsync();

        var mediaMock = CreateMediaStorageMock("/app/media/video.mp4", "video");
        var job = CreateJob(db, mediaStorage: mediaMock);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().NotContain(q => q.SocialNetwork == SocialNetwork.Youtube,
            "youtube.access_token isolado nao e mais uma credencial de configuracao valida");
    }

    [Fact]
    public async Task ExecuteAsync_NaoChamaApiML_QuandoAffiliateLinkJaPreenchido()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(platform: Platform.Amazon, affiliateLink: "https://amzn.to/existing");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var callCount = 0;
        var httpClient = CreateHttpClient(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"url\": \"x\"}") };
        });

        var job = CreateJob(db, httpClient: httpClient);
        await job.ExecuteAsync();

        callCount.Should().Be(0);

        var reloaded = await db.Products.FirstAsync();
        reloaded.AffiliateLink.Should().Be("https://amzn.to/existing");
        reloaded.Status.Should().Be(ProductStatus.Published);
    }
}
