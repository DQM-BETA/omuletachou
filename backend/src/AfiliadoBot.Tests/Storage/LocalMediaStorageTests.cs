using System.Net;
using AfiliadoBot.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AfiliadoBot.Tests.Storage;

public class LocalMediaStorageTests : IDisposable
{
    private readonly string _mediaDirectory = "/app/media";

    public void Dispose()
    {
        // Sem limpeza de /app/media entre testes: diretorio compartilhado com a app,
        // arquivos gerados usam nomes unicos (Guid) e nao colidem entre execucoes.
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

    private static HttpClient CreateThrowingHttpClient(Exception exception)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        return new HttpClient(handlerMock.Object);
    }

    private static LocalMediaStorage CreateSut(HttpClient httpClient) =>
        new(httpClient, NullLogger<LocalMediaStorage>.Instance);

    [Fact]
    public async Task DownloadAsync_RetornaPath_QuandoDownloadSucesso()
    {
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        });
        var sut = CreateSut(httpClient);

        var (localPath, mediaType) = await sut.DownloadAsync("https://cdn.example.com/foto.jpg");

        localPath.Should().NotBeNullOrWhiteSpace();
        localPath.Should().StartWith(_mediaDirectory);
        File.Exists(localPath).Should().BeTrue();
        mediaType.Should().Be("image");

        File.Delete(localPath!);
    }

    [Fact]
    public async Task DownloadAsync_RetornaNull_QuandoUrlInvalida404()
    {
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(httpClient);

        var (localPath, mediaType) = await sut.DownloadAsync("https://cdn.example.com/inexistente.jpg");

        localPath.Should().BeNull();
        mediaType.Should().Be("image");
    }

    [Fact]
    public async Task DownloadAsync_RetornaNull_QuandoTimeout()
    {
        var httpClient = CreateThrowingHttpClient(new TaskCanceledException("timeout", new TimeoutException()));
        var sut = CreateSut(httpClient);

        var (localPath, mediaType) = await sut.DownloadAsync("https://cdn.example.com/lento.jpg");

        localPath.Should().BeNull();
        mediaType.Should().Be("image");
    }

    [Fact]
    public async Task DownloadAsync_RetornaNull_QuandoUrlMalformada()
    {
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sut = CreateSut(httpClient);

        var (localPath, mediaType) = await sut.DownloadAsync("nao-e-uma-url");

        localPath.Should().BeNull();
        mediaType.Should().Be("image");
    }

    [Theory]
    [InlineData("https://cdn.example.com/video.mp4")]
    [InlineData("https://cdn.example.com/video.webm")]
    public async Task DownloadAsync_DetectaTipoVideo_QuandoExtensaoMp4OuWebm(string url)
    {
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        });
        var sut = CreateSut(httpClient);

        var (localPath, mediaType) = await sut.DownloadAsync(url);

        mediaType.Should().Be("video");
        localPath.Should().NotBeNullOrWhiteSpace();

        File.Delete(localPath!);
    }

    [Theory]
    [InlineData("https://cdn.example.com/foto.jpg")]
    [InlineData("https://cdn.example.com/foto.png")]
    [InlineData("https://cdn.example.com/foto")]
    public async Task DownloadAsync_DetectaTipoImagem_QuandoOutraExtensao(string url)
    {
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        });
        var sut = CreateSut(httpClient);

        var (localPath, mediaType) = await sut.DownloadAsync(url);

        mediaType.Should().Be("image");
        localPath.Should().NotBeNullOrWhiteSpace();

        File.Delete(localPath!);
    }
}
