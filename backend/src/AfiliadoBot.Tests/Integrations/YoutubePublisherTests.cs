using System.Net;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Integrations.Social;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AfiliadoBot.Tests.Integrations;

public class YoutubePublisherTests
{
    private const string UploadUrl = "https://upload.example.com/upload/session123";

    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static async Task SeedCredentialsAsync(
        AfiliadoBotDbContext db,
        string clientId = "cid",
        string clientSecret = "csecret",
        string refreshToken = "rtoken")
    {
        db.AppSettings.Add(new AppSetting("youtube.client_id", clientId));
        db.AppSettings.Add(new AppSetting("youtube.client_secret", clientSecret));
        db.AppSettings.Add(new AppSetting("youtube.refresh_token", refreshToken));
        await db.SaveChangesAsync();
    }

    private static Product CriarProduto(
        string? mediaLocalPath,
        string? mediaUrl,
        string? mediaType,
        string category = "Geral",
        string title = "Produto Teste")
    {
        var product = new Product(
            title: title,
            description: "Descricao",
            salePrice: 100m,
            originalPrice: 120m,
            discountPct: 10m,
            affiliateLink: "https://link.teste",
            slug: Guid.NewGuid().ToString(),
            category: category,
            platform: Platform.Amazon,
            mediaUrl: mediaUrl,
            mediaType: mediaType);

        product.UpdateAiResult(8, "Bom desconto", "Legenda de teste gerada por IA");
        product.SetLocalMedia(mediaLocalPath, mediaType);
        return product;
    }

    private static string CreateTempVideoFile(int sizeBytes = 1024)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        File.WriteAllBytes(path, new byte[sizeBytes]);
        return path;
    }

    /// <summary>
    /// Mock de HttpMessageHandler que responde de forma diferente conforme o endpoint chamado:
    /// oauth2 (refresh), upload resumable (POST inicial) e chunk (PUT). Captura todas as
    /// requisicoes para inspecao (ex.: verificar tamanho/numero de chunks).
    /// </summary>
    private static (HttpClient Client, List<HttpRequestMessage> Requests) CreateHttpClient(
        bool refreshFails = false,
        bool initiateFails = false,
        string accessToken = "new-access-token",
        bool initiateThrowsCanceled = false,
        bool chunkThrowsCanceled = false)
    {
        var requests = new List<HttpRequestMessage>();
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
            {
                // Clona a requisicao para inspecao posterior (o conteudo original e descartado
                // apos o envio).
                requests.Add(req);

                if (req.RequestUri!.Host == "oauth2.googleapis.com")
                {
                    if (refreshFails)
                        return new HttpResponseMessage(HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent("{\"error\":\"invalid_grant\"}"),
                        };

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"access_token\":\"{accessToken}\",\"expires_in\":3600}}"),
                    };
                }

                if (req.RequestUri.ToString().Contains("uploadType=resumable"))
                {
                    if (initiateFails)
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);

                    // CA15: simula o timeout total (15min) sendo atingido durante o POST de
                    // inicio do upload resumable — o proprio SDK/HttpClient lanca
                    // OperationCanceledException quando o CancellationToken (totalCts.Token no
                    // codigo de producao) e cancelado nesse ponto. `InitiateResumableUploadAsync`
                    // usa totalCts.Token diretamente (nunca chunkCts), entao esse e exatamente o
                    // caminho de codigo real percorrido quando o timeout TOTAL estoura antes do
                    // primeiro chunk.
                    if (initiateThrowsCanceled)
                        throw new TaskCanceledException("Simulacao: timeout total atingido durante o POST de inicio do upload.");

                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Headers.Location = new Uri(UploadUrl);
                    return response;
                }

                if (req.RequestUri.ToString().StartsWith(UploadUrl))
                {
                    // CA14: simula o timeout de chunk (5min) sendo atingido durante o PUT de um
                    // chunk — caminho de codigo real percorrido por `UploadChunksAsync` quando o
                    // chunkCts (linkado ao totalCts, porem com seu proprio CancelAfter(5min))
                    // cancela antes do totalCts. Nao esperamos os 5 minutos reais: o
                    // OperationCanceledException lancado aqui e identico, do ponto de vista do
                    // codigo de producao, ao lancado organicamente pelo CancellationTokenSource
                    // quando o timeout realmente decorre — o codigo de producao nao distingue a
                    // origem, apenas inspeciona `ct.IsCancellationRequested`/`totalCts.IsCancellationRequested`.
                    if (chunkThrowsCanceled)
                        throw new TaskCanceledException("Simulacao: timeout de chunk atingido durante o PUT de um chunk.");

                    var contentRange = req.Content!.Headers.ContentRange!;
                    var isLastChunk = contentRange.To == contentRange.Length!.Value - 1;

                    return isLastChunk
                        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"video123\"}") }
                        : new HttpResponseMessage((HttpStatusCode)308);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        return (new HttpClient(handlerMock.Object), requests);
    }

    private static Mock<IMediaStorage> CreateMediaStorageMock(string? localPath, string mediaType = "video")
    {
        var mock = new Mock<IMediaStorage>();
        mock.Setup(m => m.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((localPath, mediaType));
        return mock;
    }

    /// <summary>
    /// Parseia o corpo (JSON) da requisicao de inicio do upload resumable (POST
    /// uploadType=resumable) — usado pelos testes de metadata (CA5/CA6/CA7/CA10).
    /// </summary>
    private static async Task<JsonDocument> ParseMetadataAsync(List<HttpRequestMessage> requests)
    {
        var initiateRequest = requests.First(r => r.RequestUri!.ToString().Contains("uploadType=resumable"));
        var body = await initiateRequest.Content!.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    [Fact]
    public async Task PublishAsync_FalhaSemRetry_QuandoProdutoSemVideo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: "image");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _) = CreateHttpClient();
        var mediaStorage = CreateMediaStorageMock(null);
        var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.ErrorMessage.Should().Be("Produto sem mídia de vídeo, não aplicável ao YouTube");
        item.CanRetry.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_RenovaAccessToken_ViaRefreshToken()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, requests) = CreateHttpClient(accessToken: "renewed-token-xyz");
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            requests.Should().Contain(r => r.RequestUri!.Host == "oauth2.googleapis.com");

            var uploadRequest = requests.First(r => r.RequestUri!.ToString().Contains("uploadType=resumable"));
            uploadRequest.Headers.Authorization!.Parameter.Should().Be("renewed-token-xyz");

            var persisted = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "youtube.access_token");
            persisted.Should().NotBeNull();
            persisted!.Value.Should().Be("renewed-token-xyz");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_FalhaSemRetry_QuandoRefreshTokenInvalido()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _) = CreateHttpClient(refreshFails: true);
        var mediaStorage = CreateMediaStorageMock(null);
        var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

        var result = await publisher.PublishAsync(item);

        result.Should().BeFalse();
        item.Status.Should().Be(PublicationStatus.Failed);
        item.CanRetry.Should().BeFalse();

        var flag = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "youtube.token_invalid");
        flag.Should().NotBeNull();
        flag!.Value.Should().Be("true");
    }

    [Fact]
    public async Task PublishAsync_UsaMediaLocalPath_QuandoPresente()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, _) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            mediaStorage.Verify(m => m.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_BaixaMediaUrl_QuandoMediaLocalPathNulo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/video.mp4", mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, _) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(tempFile);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            mediaStorage.Verify(
                m => m.DownloadAsync("https://cdn.teste/video.mp4", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("Eletrônicos", "28")]
    [InlineData("Casa e Cozinha", "26")]
    [InlineData("Beleza e Cuidados Pessoais", "26")]
    [InlineData("Moda", "26")]
    [InlineData("Brinquedos", "24")]
    [InlineData("Geral", "22")]
    [InlineData("Categoria Inexistente", "22")]
    public async Task PublishAsync_MapeiaCategoriaCorreta_PorCategoriaDoProduto(string categoria, string categoryIdEsperado)
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video", category: categoria);
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, requests) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();

            var initiateRequest = requests.First(r => r.RequestUri!.ToString().Contains("uploadType=resumable"));
            var body = await initiateRequest.Content!.ReadAsStringAsync();
            body.Should().Contain($"\"categoryId\":\"{categoryIdEsperado}\"");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_LancaException_QuandoCredenciaisAusentes()
    {
        using var db = CreateInMemoryContext();
        // Sem seed de credenciais

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _) = CreateHttpClient();
        var mediaStorage = CreateMediaStorageMock(null);
        var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

        var act = async () => await publisher.PublishAsync(item);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PublishAsync_UploadPorChunks_RespeitaTamanhoDe8MB()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        const int chunkSize = 8 * 1024 * 1024;
        var tempFile = CreateTempVideoFile(sizeBytes: chunkSize + 1024); // pouco mais de 1 chunk

        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, requests) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();

            var chunkRequests = requests
                .Where(r => r.RequestUri!.ToString().StartsWith(UploadUrl))
                .ToList();

            chunkRequests.Should().HaveCount(2, "arquivo de 8MB+1KB deve ser enviado em 2 chunks");
            chunkRequests[0].Content!.Headers.ContentLength.Should().Be(chunkSize);
            chunkRequests[1].Content!.Headers.ContentLength.Should().Be(1024);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_TruncaTitulo_Para100CaracteresNoMetadata()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var tituloLongo = string.Concat(Enumerable.Range(0, 15).Select(i => $"Palavra{i:D2}-")); // > 100 chars
            tituloLongo.Length.Should().BeGreaterThan(100);

            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video", title: tituloLongo);
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, requests) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();

            using var metadata = await ParseMetadataAsync(requests);
            var title = metadata.RootElement.GetProperty("snippet").GetProperty("title").GetString();

            title.Should().Be(tituloLongo[..100]);
            title!.Length.Should().Be(100);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_DescricaoNoMetadata_IgualAoAiCaption()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, requests) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            product.AiCaption.Should().NotBeNullOrWhiteSpace();

            using var metadata = await ParseMetadataAsync(requests);
            var description = metadata.RootElement.GetProperty("snippet").GetProperty("description").GetString();

            description.Should().Be(product.AiCaption);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_TagsNoMetadata_ContemValoresFixosEsperados()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, requests) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();

            using var metadata = await ParseMetadataAsync(requests);
            var tags = metadata.RootElement.GetProperty("snippet").GetProperty("tags")
                .EnumerateArray()
                .Select(t => t.GetString())
                .ToArray();

            tags.Should().BeEquivalentTo(new[] { "oferta", "desconto", "promocao", "youtube" }, options => options.WithStrictOrdering());
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_PrivacyStatusNoMetadata_SempreIgualAPublic()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, requests) = CreateHttpClient();
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();

            using var metadata = await ParseMetadataAsync(requests);
            var privacyStatus = metadata.RootElement.GetProperty("status").GetProperty("privacyStatus").GetString();

            privacyStatus.Should().Be("public");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_TimeoutDeChunk_LancaInvalidOperationExceptionComMensagemDeChunk()
    {
        // CA14: simula o timeout de chunk (5min) sendo atingido — ver comentario em
        // CreateHttpClient(chunkThrowsCanceled) para a justificativa de nao esperar os 5
        // minutos reais. `UploadChunksAsync` (codigo de producao) captura a
        // OperationCanceledException lancada pelo HttpClient e, como `ct` (CancellationToken
        // externo, aqui default/None) nunca foi cancelado e o `totalCts` interno tambem nao foi
        // realmente cancelado, cai exatamente no branch "timeout de chunk" (nao "timeout
        // total") — o mesmo branch percorrido quando o `ChunkTimeout` de 5min realmente estoura
        // antes do `TotalTimeout` de 15min.
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, _) = CreateHttpClient(chunkThrowsCanceled: true);
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var act = async () => await publisher.PublishAsync(item);

            var assertion = await act.Should().ThrowAsync<InvalidOperationException>();
            assertion.Which.Message.Should().Contain("timeout de chunk");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_TimeoutTotal_LancaInvalidOperationExceptionComMensagemDeTimeoutTotal()
    {
        // CA15: simula o timeout total (15min) sendo atingido durante o POST de inicio do
        // upload resumable — ver comentario em CreateHttpClient(initiateThrowsCanceled) para a
        // justificativa de nao esperar os 15 minutos reais. `InitiateResumableUploadAsync`
        // (codigo de producao) usa `totalCts.Token` diretamente (nunca `chunkCts`), entao a
        // OperationCanceledException lancada aqui cai, de forma incondicional, no branch
        // "timeout total" — o mesmo branch percorrido quando o `TotalTimeout` de 15min
        // realmente estoura antes de qualquer chunk ser enviado.
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = CreateTempVideoFile();
        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: null, mediaType: "video");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, _) = CreateHttpClient(initiateThrowsCanceled: true);
            var mediaStorage = CreateMediaStorageMock(null);
            var publisher = new YoutubePublisher(httpClient, db, mediaStorage.Object, NullLogger<YoutubePublisher>.Instance);

            var act = async () => await publisher.PublishAsync(item);

            var assertion = await act.Should().ThrowAsync<InvalidOperationException>();
            assertion.Which.Message.Should().Contain("timeout total");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
