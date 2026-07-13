using System.Net;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Integrations.Social;
using AfiliadoBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AfiliadoBot.Tests.Integrations;

public class TikTokPublisherTests
{
    private const string PublishId = "publish-123";
    private const string UploadUrl = "https://open-upload.tiktokapis.com/video/upload-abc";

    private static readonly TimeSpan[] FastRetryDelays =
        { TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1) };
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
        string clientKey = "client-key",
        string clientSecret = "client-secret",
        string refreshToken = "refresh-token",
        string accessToken = "access-token",
        string privacyLevel = "SELF_ONLY",
        int minDuration = 3,
        int maxDuration = 600)
    {
        db.AppSettings.Add(new AppSetting("tiktok.client_key", clientKey));
        db.AppSettings.Add(new AppSetting("tiktok.client_secret", clientSecret));
        db.AppSettings.Add(new AppSetting("tiktok.refresh_token", refreshToken));
        db.AppSettings.Add(new AppSetting("tiktok.access_token", accessToken));
        db.AppSettings.Add(new AppSetting("tiktok.privacy_level", privacyLevel));
        db.AppSettings.Add(new AppSetting("tiktok.min_duration_seconds", minDuration.ToString()));
        db.AppSettings.Add(new AppSetting("tiktok.max_duration_seconds", maxDuration.ToString()));
        await db.SaveChangesAsync();
    }

    private static Product CriarProduto(string? mediaLocalPath, string? mediaUrl, string? mediaType, string? aiCaption = "Legenda gerada por IA")
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
    /// Mock de HttpMessageHandler que roteia por host/path/metodo: refresh de token
    /// (<c>oauth/token</c>), etapa 1 (<c>video/init</c>), etapa 2 (PUT na upload_url) e etapa 3
    /// (<c>publish/status/fetch</c>). Suporta 401 unico (refresh) e N respostas 429 antes de
    /// suceder/falhar, para exercitar CA16/CA17.
    /// </summary>
    private static (HttpClient Client, List<HttpRequestMessage> Requests) CreateHttpClient(
        bool initFails = false,
        int init429Count = 0,
        bool initUnauthorizedOnce = false,
        string uploadUrl = UploadUrl,
        string publishId = PublishId,
        bool uploadFails = false,
        int upload429Count = 0,
        Queue<string>? pollStatusSequence = null,
        bool pollUnauthorizedOnce = false,
        bool refreshFails = false,
        string refreshedAccessToken = "refreshed-token")
    {
        var requests = new List<HttpRequestMessage>();
        var handlerMock = new Mock<HttpMessageHandler>();
        var statusQueue = pollStatusSequence ?? new Queue<string>(new[] { "PUBLISH_COMPLETE" });
        var initCallCount = 0;
        var uploadCallCount = 0;
        var pollCallCount = 0;

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

                if (url.Contains("oauth/token"))
                {
                    if (refreshFails)
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent("{\"error\":\"invalid_grant\"}"),
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            $"{{\"access_token\":\"{refreshedAccessToken}\",\"refresh_token\":\"new-refresh-token\",\"expires_in\":86400}}"),
                    };
                }

                if (url.Contains("video/init"))
                {
                    initCallCount++;

                    if (initUnauthorizedOnce && initCallCount == 1)
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                    if (initCallCount <= init429Count)
                        return new HttpResponseMessage((HttpStatusCode)429);

                    if (initFails)
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent("{\"error\":{\"message\":\"invalid params\"}}"),
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            $"{{\"data\":{{\"publish_id\":\"{publishId}\",\"upload_url\":\"{uploadUrl}\"}}}}"),
                    };
                }

                if (req.Method == HttpMethod.Put && string.Equals(url, uploadUrl, StringComparison.Ordinal))
                {
                    uploadCallCount++;

                    if (uploadCallCount <= upload429Count)
                        return new HttpResponseMessage((HttpStatusCode)429);

                    if (uploadFails)
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);

                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                if (url.Contains("publish/status/fetch"))
                {
                    pollCallCount++;

                    if (pollUnauthorizedOnce && pollCallCount == 1)
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                    var status = statusQueue.Count > 1 ? statusQueue.Dequeue() : statusQueue.Peek();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"data\":{{\"status\":\"{status}\"}}}}"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        return (new HttpClient(handlerMock.Object), requests);
    }

    private static TikTokPublisher CreatePublisher(
        HttpClient httpClient,
        AfiliadoBotDbContext db,
        IMediaStorage? mediaStorage = null,
        TimeSpan[]? retryDelays = null,
        TimeSpan? pollInterval = null,
        TimeSpan? pollTimeout = null)
    {
        return new TikTokPublisher(
            httpClient,
            db,
            mediaStorage ?? Mock.Of<IMediaStorage>(),
            NullLogger<TikTokPublisher>.Instance,
            retryDelays ?? FastRetryDelays,
            pollInterval ?? FastPollInterval,
            pollTimeout ?? FastPollTimeout);
    }

    private static async Task<PublicationQueue> SeedProductAndQueueAsync(AfiliadoBotDbContext db, Product product)
    {
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.TikTok, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    // ---- CA1/CA2/CA3/CA11: fluxo feliz completo (init, upload, polling) -------------------------

    [Fact]
    public async Task PublishAsync_ExecutaAs3Etapas_QuandoFluxoFeliz()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();

            var initRequest = requests.Single(r => r.RequestUri!.ToString().Contains("video/init"));
            var initBody = await initRequest.Content!.ReadAsStringAsync();
            initBody.Should().Contain("\"brand_content_toggle\":true");
            initBody.Should().Contain("\"disable_duet\":false");
            initBody.Should().Contain("\"disable_comment\":false");
            initBody.Should().Contain("\"privacy_level\":\"SELF_ONLY\"");
            initBody.Should().Contain("\"source\":\"FILE_UPLOAD\"");

            var uploadRequest = requests.Single(r => r.Method == HttpMethod.Put);
            uploadRequest.Content!.Headers.ContentRange.Should().NotBeNull();
            uploadRequest.Content.Headers.ContentLength.Should().NotBeNull();

            requests.Should().Contain(r => r.RequestUri!.ToString().Contains("publish/status/fetch"));
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_EnviaMultiplosChunks_QuandoArquivoMaiorQueUmChunk()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        // Forca 2 chunks de 8MB (arquivo com > 8MB).
        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10, minTotalBytes: 9 * 1024 * 1024);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            var putRequests = requests.Where(r => r.Method == HttpMethod.Put).ToList();
            putRequests.Should().HaveCount(2);
            putRequests[0].Content!.Headers.ContentRange!.From.Should().Be(0);
            putRequests[1].Content!.Headers.ContentRange!.From.Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_PollingContinuaAtePublishComplete_QuandoProcessingUpload()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var statusSequence = new Queue<string>(new[] { "PROCESSING_UPLOAD", "PROCESSING_UPLOAD", "PUBLISH_COMPLETE" });
            var (httpClient, requests) = CreateHttpClient(pollStatusSequence: statusSequence);
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            requests.Count(r => r.RequestUri!.ToString().Contains("publish/status/fetch")).Should().Be(3);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA4: polling FAILED ---------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_FalhaImediatamente_QuandoStatusFailed()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var statusSequence = new Queue<string>(new[] { "FAILED" });
            var (httpClient, _) = CreateHttpClient(pollStatusSequence: statusSequence);
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeFalse();
            item.Status.Should().Be(PublicationStatus.Failed);
            item.CanRetry.Should().BeTrue();
            item.ErrorMessage.Should().Contain("FAILED");
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA5: timeout de polling (10 minutos) ------------------------------------------------------

    [Fact]
    public async Task PublishAsync_MarcaFailedComRetry_QuandoTimeoutDePolling()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            // Sempre PROCESSING_UPLOAD — nunca atinge PUBLISH_COMPLETE, forcando o timeout curto de teste.
            var statusSequence = new Queue<string>(new[] { "PROCESSING_UPLOAD", "PROCESSING_UPLOAD" });
            var (httpClient, _) = CreateHttpClient(pollStatusSequence: statusSequence);
            var publisher = CreatePublisher(
                httpClient, db, pollInterval: TimeSpan.FromMilliseconds(5), pollTimeout: TimeSpan.FromMilliseconds(30));

            var result = await publisher.PublishAsync(item);

            result.Should().BeFalse();
            item.Status.Should().Be(PublicationStatus.Failed);
            item.CanRetry.Should().BeTrue("timeout de polling permite retry, diferente do FailPermanently");
            item.RetryCount.Should().Be(1);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA6/CA7/CA8/CA9: validacao de duracao -----------------------------------------------------

    [Fact]
    public async Task PublishAsync_PassaNaValidacao_QuandoDuracaoDentroDoIntervalo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, minDuration: 3, maxDuration: 600);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(300);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            requests.Should().Contain(r => r.RequestUri!.ToString().Contains("video/init"));
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_FailPermanently_QuandoDuracaoAbaixoDoMinimo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, minDuration: 3, maxDuration: 600);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(2);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeFalse();
            item.Status.Should().Be(PublicationStatus.Failed);
            item.CanRetry.Should().BeFalse();
            item.ErrorMessage.Should().Be("Vídeo fora do intervalo de duração aceito pelo TikTok (3s-10min)");
            requests.Should().BeEmpty();
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_FailPermanently_QuandoDuracaoAcimaDoMaximo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, minDuration: 3, maxDuration: 600);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(700);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeFalse();
            item.Status.Should().Be(PublicationStatus.Failed);
            item.CanRetry.Should().BeFalse();
            item.ErrorMessage.Should().Be("Vídeo fora do intervalo de duração aceito pelo TikTok (3s-10min)");
            requests.Should().BeEmpty();
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_RespeitaLimitesParametrizados_QuandoAppSettingsDiferentesDoSeed()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, minDuration: 5, maxDuration: 300);

        // 310s: dentro do seed padrao (3-600), mas fora dos limites parametrizados (5-300).
        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(310);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeFalse();
            item.ErrorMessage.Should().Be("Vídeo fora do intervalo de duração aceito pelo TikTok (5s-5min)");
            requests.Should().BeEmpty();
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA10: proporcao/resolucao nao sao validadas client-side -----------------------------------

    [Fact]
    public async Task PublishAsync_NaoValidaProporcaoOuResolucao_SeguindoParaUpload()
    {
        // Nao ha campo de proporcao/resolucao no dominio nem checagem no TikTokPublisher — o
        // teste documenta o comportamento (fluxo prossegue independentemente do "formato" do
        // arquivo), unico ponto validado client-side e a duracao (CA6-CA9).
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(15);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, _) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA12/CA13: disclosure duplo ----------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_AnexaHashtagPubli_QuandoLegendaNaoContem()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video", aiCaption: "Confira essa oferta incrivel!");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            await publisher.PublishAsync(item);

            var initRequest = requests.Single(r => r.RequestUri!.ToString().Contains("video/init"));
            var body = await initRequest.Content!.ReadAsStringAsync();
            body.Should().Contain("Confira essa oferta incrivel! #publi");
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Theory]
    [InlineData("Confira a oferta #publi")]
    [InlineData("Confira a oferta #publicidade")]
    [InlineData("Confira a oferta #Publi")]
    public async Task PublishAsync_NaoDuplicaDisclosure_QuandoJaPresente(string captionComDisclosure)
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video", aiCaption: captionComDisclosure);
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            await publisher.PublishAsync(item);

            var initRequest = requests.Single(r => r.RequestUri!.ToString().Contains("video/init"));
            var body = await initRequest.Content!.ReadAsStringAsync();

            var occurrences = System.Text.RegularExpressions.Regex.Matches(
                body, "#publi", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            occurrences.Should().Be(1);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA14/CA15: privacy_level configuravel ------------------------------------------------------

    [Fact]
    public async Task PublishAsync_EnviaSelfOnly_QuandoPrivacyLevelSeedPadrao()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, privacyLevel: "SELF_ONLY");

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            await publisher.PublishAsync(item);

            var initRequest = requests.Single(r => r.RequestUri!.ToString().Contains("video/init"));
            var body = await initRequest.Content!.ReadAsStringAsync();
            body.Should().Contain("\"privacy_level\":\"SELF_ONLY\"");
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_EnviaPublicToEveryone_QuandoConfiguradoManualmente()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, privacyLevel: "PUBLIC_TO_EVERYONE");

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db);

            await publisher.PublishAsync(item);

            var initRequest = requests.Single(r => r.RequestUri!.ToString().Contains("video/init"));
            var body = await initRequest.Content!.ReadAsStringAsync();
            body.Should().Contain("\"privacy_level\":\"PUBLIC_TO_EVERYONE\"");
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA16: refresh automatico em 401 --------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_RenovaTokenERepeteChamada_Quando401NoInit()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, accessToken: "token-expirado");

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, requests) = CreateHttpClient(initUnauthorizedOnce: true, refreshedAccessToken: "novo-token");
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            requests.Should().Contain(r => r.RequestUri!.ToString().Contains("oauth/token"));

            var initRequests = requests.Where(r => r.RequestUri!.ToString().Contains("video/init")).ToList();
            initRequests.Should().HaveCount(2);
            initRequests[1].Headers.Authorization!.Parameter.Should().Be("novo-token");

            var persisted = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "tiktok.access_token");
            persisted!.Value.Should().Be("novo-token");
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_FailPermanently_QuandoRenovacaoDeTokenFalha()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db, accessToken: "token-expirado");

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var (httpClient, _) = CreateHttpClient(initUnauthorizedOnce: true, refreshFails: true);
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeFalse();
            item.Status.Should().Be(PublicationStatus.Failed);
            item.CanRetry.Should().BeFalse();
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- CA17/CA18: backoff exponencial em 429 e RetryCount ---------------------------------------------

    [Fact]
    public async Task PublishAsync_TentaNovamenteAposBackoff_Quando429NoInit()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            // 2 respostas 429 e depois sucesso — dentro do orcamento de 3 tentativas (2s/4s/8s
            // reais, aqui injetadas como 1ms para o teste ser rapido).
            var (httpClient, requests) = CreateHttpClient(init429Count: 2);
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            requests.Count(r => r.RequestUri!.ToString().Contains("video/init")).Should().Be(3);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_FalhaComRetryPadrao_QuandoBackoff429Esgotado()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            // Sempre 429 — esgota as 3 tentativas de backoff.
            var (httpClient, requests) = CreateHttpClient(init429Count: 999);
            var publisher = CreatePublisher(httpClient, db);

            var result = await publisher.PublishAsync(item);

            result.Should().BeFalse();
            item.Status.Should().Be(PublicationStatus.Failed);
            item.CanRetry.Should().BeTrue("429 esgotado e recuperavel pelo PublisherJob, diferente do FailPermanently");
            requests.Count(r => r.RequestUri!.ToString().Contains("video/init")).Should().Be(4); // 1 + 3 retries
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task PublishAsync_RetryCountAtingeMaximo_AposTresFalhasRecuperaveisConsecutivas()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: videoPath, mediaUrl: null, mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var statusSequence = new Queue<string>(new[] { "FAILED" });
            var (httpClient, _) = CreateHttpClient(pollStatusSequence: statusSequence);
            var publisher = CreatePublisher(httpClient, db);

            for (var i = 0; i < 3; i++)
                await publisher.PublishAsync(item);

            item.RetryCount.Should().Be(3);
            item.Status.Should().Be(PublicationStatus.Failed);
            item.CanRetry.Should().BeFalse("RetryCount atingiu o maximo de 3 tentativas (CA18)");
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    // ---- Fallback de seguranca / credenciais / produto ausentes -------------------------------------

    [Fact]
    public async Task PublishAsync_FailPermanently_QuandoProdutoSemVideo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: "image");
        var item = await SeedProductAndQueueAsync(db, product);

        var (httpClient, requests) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.ErrorMessage.Should().Be("Produto sem mídia de vídeo, não aplicável ao TikTok");
        item.CanRetry.Should().BeFalse();
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_LancaException_QuandoCredenciaisAusentes()
    {
        using var db = CreateInMemoryContext();
        // Sem seed de credenciais.

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: "video");
        var item = await SeedProductAndQueueAsync(db, product);

        var (httpClient, _) = CreateHttpClient();
        var publisher = CreatePublisher(httpClient, db);

        var act = async () => await publisher.PublishAsync(item);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PublishAsync_BaixaMidiaViaMediaStorage_QuandoMediaLocalPathAusente()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var videoPath = Mp4TestFileBuilder.CreateFileWithDuration(10);
        try
        {
            var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
            var item = await SeedProductAndQueueAsync(db, product);

            var mediaStorageMock = new Mock<IMediaStorage>();
            mediaStorageMock
                .Setup(m => m.DownloadAsync("https://cdn.teste/video.mp4", It.IsAny<CancellationToken>()))
                .ReturnsAsync((videoPath, "video"));

            var (httpClient, requests) = CreateHttpClient();
            var publisher = CreatePublisher(httpClient, db, mediaStorage: mediaStorageMock.Object);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            requests.Should().Contain(r => r.RequestUri!.ToString().Contains("video/init"));
        }
        finally
        {
            // Nao deleta videoPath aqui: TikTokPublisher e responsavel por apagar o arquivo
            // temporario "baixado" ao final do processamento (mesmo padrao do YoutubePublisher).
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }
    }
}
