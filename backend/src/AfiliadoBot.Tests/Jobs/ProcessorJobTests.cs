using System.Net;
using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.DTOs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    // Issue #182/#184: ProcessorJob nao chama mais nenhuma API para resolver o link de afiliado
    // ML (fluxo semi-manual) — este client "default" nunca e efetivamente invocado pelo job; e
    // apenas o valor satisfazendo o parametro HttpClient ainda presente no construtor.
    private static HttpClient CreateAffiliateLinkClient() =>
        CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"url\": \"https://ml.link/aff\"}")
        });

    private static Mock<IMediaStorage> CreateMediaStorageMock(string? localPath = "/app/media/abc.jpg", string mediaType = "image")
    {
        var mock = new Mock<IMediaStorage>();
        mock.Setup(m => m.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((localPath, mediaType));
        return mock;
    }

    private static Mock<IAiService> CreateAiServiceMock()
    {
        // CA16: retorna uma legenda diferente por rede (nao um valor fixo), tornando o mock
        // determinístico o suficiente para comprovar que cada item de PublicationQueue recebe a
        // Caption correspondente à sua própria rede, sem sobrescrita.
        var mock = new Mock<IAiService>();
        mock.Setup(a => a.GenerateCaptionAsync(It.IsAny<Product>(), It.IsAny<SocialNetwork>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product _, SocialNetwork network, CancellationToken _) => $"Legenda {network}");
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
            new PassThroughJobRunTracker(),
            NullLogger<ProcessorJob>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_MarcaProcessing_AoIniciarProcessamento()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: null);
        db.Products.Add(product);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
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
    public async Task ExecuteAsync_NaoAlteraCategoria_QuandoFallbackIaNaoClassifica()
    {
        // A categorizacao por dicionario (CategoryDetector) saiu do ProcessorJob e passou a
        // rodar nos collectors, na criacao do Product (Issue #167, Sub-A/#168). O fallback via
        // IA (Sub-B/#169) roda aqui, mas quando ClassifyCategoryAsync retorna null (orcamento
        // estourado ou erro/timeout — CA 4.3, design.md §3.6) o produto permanece "Geral". O
        // mock de IAiService (CreateAiServiceMock) nao configura ClassifyCategoryAsync, entao
        // Moq devolve o default (Task<CategoryClassification?> completo com null).
        using var db = CreateInMemoryContext();
        var product = CriarProduto(title: "Fone de Ouvido Bluetooth", category: "Geral");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Category.Should().Be("Geral");
        reloaded.Subcategory.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ChamaFallbackIa_QuandoCategoriaGeral_EAtualizaCategoria()
    {
        // CA 3.1: produto Queued + Category == "Geral" -> fallback IA acionado, e o resultado
        // sobrescreve Category/Subcategory do produto.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(title: "Produto sem match no dicionario", category: "Geral");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        aiMock.Setup(a => a.ClassifyCategoryAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CategoryClassification("Eletrônicos", "Celulares e Smartphones"));

        var job = CreateJob(db, aiService: aiMock);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Category.Should().Be("Eletrônicos");
        reloaded.Subcategory.Should().Be("Celulares e Smartphones");
        aiMock.Verify(a => a.ClassifyCategoryAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NaoChamaFallbackIa_QuandoDicionarioJaClassificou()
    {
        // CA 3.3: Category != "Geral" (dicionario ja classificou na coleta) -> fallback IA nunca
        // e chamado, nenhum custo e gerado.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(category: "Eletrônicos");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var job = CreateJob(db, aiService: aiMock);
        await job.ExecuteAsync();

        aiMock.Verify(a => a.ClassifyCategoryAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);

        var reloaded = await db.Products.FirstAsync();
        reloaded.Category.Should().Be("Eletrônicos");
    }

    [Fact]
    public async Task ExecuteAsync_ChamaFallbackIa_AntesDeGerarSlug()
    {
        // CA 3.1 / design.md §3.6: o fallback precisa rodar ANTES de EnsureSlug — verificado
        // capturando o estado do Slug no momento exato em que ClassifyCategoryAsync e chamado.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(title: "Produto Sem Categoria E Sem Slug", category: "Geral", slug: "");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        string? slugNoMomentoDoFallback = "valor-nao-capturado";
        var aiMock = CreateAiServiceMock();
        aiMock.Setup(a => a.ClassifyCategoryAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken _) =>
            {
                slugNoMomentoDoFallback = p.Slug;
                return new CategoryClassification("Eletrônicos", "Celulares e Smartphones");
            });

        var job = CreateJob(db, aiService: aiMock);
        await job.ExecuteAsync();

        slugNoMomentoDoFallback.Should().BeEmpty();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Slug.Should().NotBeNullOrWhiteSpace();
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

        // CA3/CA15: o retorno de GenerateCaptionAsync deve ser persistido em
        // PublicationQueue.Caption (nao descartado) — o teste valida o estado persistido, nao
        // apenas a chamada do mock.
        entries.Single(e => e.SocialNetwork == SocialNetwork.Telegram).Caption
            .Should().Be("Legenda Telegram");
    }

    [Fact]
    public async Task ExecuteAsync_PersisteCaptionDistintaPorRede_QuandoMultiplasRedesHabilitadas()
    {
        // CA4/CA16: produto agendado para 2+ redes com prompts/textos diferentes por rede —
        // cada item de PublicationQueue deve ter a Caption correspondente à sua própria rede,
        // sem um item sobrescrever o outro (o bug original usava Product.AiCaption, campo único
        // sobrescrito a cada chamada).
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: "https://cdn.com/video.mp4");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await SeedNetworkAsync(db, "instagram", true, ("instagram.access_token", "tok"), ("instagram.page_id", "1"));
        await SeedNetworkAsync(db, "tiktok", true, ("tiktok.access_token", "tok"));
        await db.SaveChangesAsync();

        var mediaMock = CreateMediaStorageMock("/app/media/video.mp4", "video");
        var job = CreateJob(db, mediaStorage: mediaMock);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();

        entries.Single(e => e.SocialNetwork == SocialNetwork.Telegram).Caption
            .Should().Be("Legenda Telegram");
        entries.Single(e => e.SocialNetwork == SocialNetwork.Instagram).Caption
            .Should().Be("Legenda Instagram");
        entries.Single(e => e.SocialNetwork == SocialNetwork.TikTok).Caption
            .Should().Be("Legenda TikTok");
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
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Published);
    }

    [Fact]
    public async Task ExecuteAsync_MarcaPublished_QuandoNenhumaRedeQualificada()
    {
        // Issue #208 (substitui o comportamento das Issues #133/#145): zero entradas de
        // PublicationQueue criadas (nenhuma rede habilitada e com credenciais completas) NAO
        // bloqueia mais a visibilidade no site — o produto deve ir para Published, nao Error.
        // A ausencia de rede qualificada nao e registrada como erro em lugar nenhum (CA 3.2).
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        // Nenhuma rede habilitada/configurada.
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Published);

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MarcaPublished_QuandoRedeHabilitadaMasSemCredenciais()
    {
        // Issue #208: rede habilitada porem sem credenciais completas continua nao qualificando
        // (nenhuma entrada de PublicationQueue e criada para ela — sem regressao na logica de
        // CreatePublicationQueueEntriesAsync), mas isso nao impede mais o produto de ser
        // publicado no site (CA 2.2/CA 3.2).
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await SeedNetworkAsync(db, "instagram", true); // habilitada, sem credenciais
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Published);

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_LogaInformacao_QuandoNenhumaRedeQualificada()
    {
        // Decisao de observabilidade (especificacao-tecnica.md §0.2): LogInformation explicito
        // quando queuedCount == 0, distinto do LogWarning das redes puladas individualmente —
        // nao e uma condicao anomala, e o comportamento correto pos-Issue #208.
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<ProcessorJob>>();
        var job = new ProcessorJob(
            db,
            CreateMediaStorageMock().Object,
            CreateAiServiceMock().Object,
            CreateAffiliateLinkClient(),
            new PassThroughJobRunTracker(),
            loggerMock.Object);

        await job.ExecuteAsync();

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("publicado no site sem nenhuma rede social qualificada")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(Platform.Amazon)]
    [InlineData(Platform.MercadoLivre)]
    [InlineData(Platform.Shopee)]
    public async Task ExecuteAsync_MarcaPublished_ECriaFila_QuandoRedeQualificada(Platform platform)
    {
        // CA 2.1 / não-regressão explícita: produto aprovado + link já válido (bypassa o fluxo
        // semi-manual ML) + rede social qualificada -> Published E entrada correspondente em
        // PublicationQueue, para as 3 plataformas de origem suportadas.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(platform: platform, affiliateLink: "https://afiliado.example.com/produto");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Published);

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().ContainSingle(e => e.SocialNetwork == SocialNetwork.Telegram);
    }

    [Fact]
    public async Task ExecuteAsync_NaoReenfileira_ProdutoJaPublicadoQuandoRedeQualificaDepois()
    {
        // CA 6.1/6.2 (não-retroatividade): produto já Published (fora do fluxo Queued) sem
        // nenhuma PublicationQueue continua sem receber novas entradas mesmo depois de uma rede
        // social ser qualificada — comportamento já emergente da query do topo de ExecuteAsync
        // (Status == Queued), coberto aqui explicitamente.
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var job = CreateJob(db);
        await job.ExecuteAsync();

        var afterFirstRun = await db.Products.FirstAsync();
        afterFirstRun.Status.Should().Be(ProductStatus.Published);
        (await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync()).Should().BeEmpty();

        // Rede qualifica depois que o produto ja foi publicado.
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
        await db.SaveChangesAsync();

        await job.ExecuteAsync();

        var afterSecondRun = await db.Products.FirstAsync();
        afterSecondRun.Status.Should().Be(ProductStatus.Published);
        (await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MarcaAwaitingAffiliateLink_QuandoProdutoMlSemLinkComSourceUrl()
    {
        // Issue #182/#184 (Gate 1.5): affiliate-tools/links nao e mais chamado — produto ML sem
        // AffiliateLink e com SourceUrl vai para AwaitingAffiliateLink (fluxo semi-manual), nao
        // Error. Nenhuma chamada HTTP e feita para resolver o link (callCount permanece 0).
        using var db = CreateInMemoryContext();
        var product = CriarProduto(
            platform: Platform.MercadoLivre,
            affiliateLink: null,
            sourceUrl: "https://www.mercadolivre.com.br/p/MLB123");
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

        callCount.Should().Be(0, "o fluxo semi-manual (Issue #182/#184) nao chama mais nenhuma API para resolver o link de afiliado");

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.AwaitingAffiliateLink);
        reloaded.AffiliateLink.Should().BeNull();

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
        // CA 3.2: produto Rejected (Status != Queued) nunca chega ao loop de ExecuteAsync (a
        // query do topo ja filtra por Queued) — o fallback de categorizacao via IA nao pode ser
        // chamado para ele, mesmo que a Category seja "Geral".
        using var db = CreateInMemoryContext();
        var product = CriarProduto(category: "Geral");
        product.UpdateAiResult(2, "Score baixo", ""); // Rejected
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var job = CreateJob(db, aiService: aiMock);
        await job.ExecuteAsync();

        var reloaded = await db.Products.FirstAsync();
        reloaded.Status.Should().Be(ProductStatus.Rejected);
        aiMock.Verify(a => a.ClassifyCategoryAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task ExecuteAsync_NaoAfetaDemaisRedes_QuandoYoutubeEInstagramFiltrados()
    {
        // CA18 (regressao, nao-negociavel — Issue #9 / #73 generaliza o filtro do Issue #8 / #65
        // para tambem cobrir Instagram): produto sem video, elegivel para Telegram/TikTok/
        // Facebook (com credenciais) — o filtro adicional de video (Youtube + Instagram) nao pode
        // afetar as demais redes, que devem se comportar exatamente como antes da correcao.
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
        entries.Should().NotContain(q => q.SocialNetwork == SocialNetwork.Instagram);
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Telegram);
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.TikTok);
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Facebook);
        entries.Single(q => q.SocialNetwork == SocialNetwork.Facebook).Status.Should().Be(PublicationStatus.ManualPending);
        entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteAsync_NaoCriaEntradaInstagram_QuandoProdutoSemVideo()
    {
        // CA16: Instagram habilitado com credenciais, mas produto sem video — nenhuma entrada
        // Instagram e criada, e GenerateCaptionAsync nao e chamado para essa rede.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: null);
        db.Products.Add(product);
        await SeedNetworkAsync(db, "instagram", true, ("instagram.access_token", "tok"), ("instagram.page_id", "1"));
        await db.SaveChangesAsync();

        var aiMock = CreateAiServiceMock();
        var job = CreateJob(db, aiService: aiMock);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().NotContain(q => q.SocialNetwork == SocialNetwork.Instagram);
        aiMock.Verify(a => a.GenerateCaptionAsync(It.IsAny<Product>(), SocialNetwork.Instagram, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CriaEntradaInstagram_QuandoProdutoComVideo()
    {
        // CA17: Instagram habilitado com credenciais e produto com video — entrada criada
        // normalmente, com ScheduledAt no slot round-robin correspondente.
        using var db = CreateInMemoryContext();
        var product = CriarProduto(mediaUrl: "https://cdn.com/video.mp4");
        db.Products.Add(product);
        await SeedNetworkAsync(db, "instagram", true, ("instagram.access_token", "tok"), ("instagram.page_id", "1"));
        await db.SaveChangesAsync();

        var mediaMock = CreateMediaStorageMock("/app/media/video.mp4", "video");
        var aiMock = CreateAiServiceMock();
        var job = CreateJob(db, mediaStorage: mediaMock, aiService: aiMock);
        await job.ExecuteAsync();

        var entries = await db.PublicationQueues.Where(q => q.ProductId == product.Id).ToListAsync();
        entries.Should().ContainSingle(q => q.SocialNetwork == SocialNetwork.Instagram);
        entries.Single(q => q.SocialNetwork == SocialNetwork.Instagram).ScheduledAt.Hour.Should().Be(9);
        aiMock.Verify(a => a.GenerateCaptionAsync(It.IsAny<Product>(), SocialNetwork.Instagram, It.IsAny<CancellationToken>()), Times.Once);
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
        await SeedNetworkAsync(db, "telegram", true, ("telegram.bot_token", "abc"), ("telegram.channel_id", "123"));
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

    // --- Issue #227 (JobRun tracking) ---

    [Fact]
    public async Task ExecuteAsync_GeraJobRun_QuandoChamadoDiretamente()
    {
        // CA 4.2/5.1: ExecuteAsync chamado diretamente (simulando o Enqueue encadeado pelo
        // CollectorJob, sem passar pelo controller) tambem gera um JobRun.
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var tracker = new JobRunTracker(db);
        var job = new ProcessorJob(
            db,
            CreateMediaStorageMock().Object,
            CreateAiServiceMock().Object,
            CreateAffiliateLinkClient(),
            tracker,
            NullLogger<ProcessorJob>.Instance);

        await job.ExecuteAsync();

        var run = await db.JobRuns.SingleAsync();
        run.JobName.Should().Be(JobName.Processor);
        run.Status.Should().Be(JobRunStatus.Success);
    }
}
