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

    // Resolvedor de DNS falso: retorna sempre um IP publico para qualquer host, mantendo a
    // suite hermetica (sem depender de resolucao de rede real para hosts como
    // "cdn.example.com") — item A3 (allowlist SSRF) e coberto por testes dedicados que
    // injetam IPs privados/loopback via este mesmo mecanismo.
    private static readonly IPAddress PublicTestIp = IPAddress.Parse("93.184.216.34");

    private static LocalMediaStorage CreateSut(HttpClient httpClient, IPAddress? resolvedIp = null) =>
        new(httpClient, NullLogger<LocalMediaStorage>.Instance,
            (_, _) => Task.FromResult(new[] { resolvedIp ?? PublicTestIp }));

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

    // --- SSRF allowlist (Issue #133 / #145, item A3) ---

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://10.0.0.5/foto.jpg")]
    [InlineData("http://172.16.0.5/foto.jpg")]
    [InlineData("http://192.168.1.5/foto.jpg")]
    [InlineData("http://127.0.0.1/foto.jpg")]
    public async Task DownloadAsync_RetornaNull_QuandoUrlApontaParaIpLiteralPrivado(string url)
    {
        // URLs cujo host ja e um IP literal (nao passa pelo resolvedor fake) — cobre a
        // checagem direta de IPAddress.TryParse antes de qualquer chamada de DNS.
        var httpCalled = false;
        var httpClient = CreateHttpClient(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
        });
        var sut = CreateSut(httpClient);

        var (localPath, _) = await sut.DownloadAsync(url);

        localPath.Should().BeNull();
        httpCalled.Should().BeFalse("a allowlist deve bloquear antes de qualquer chamada HTTP");
    }

    [Fact]
    public async Task DownloadAsync_RetornaNull_QuandoHostnameLocalhostResolveParaLoopback()
    {
        // "localhost" nao e um IP literal — passa pelo resolvedor (fake, aqui simulando o
        // comportamento real de resolver para 127.0.0.1).
        var httpCalled = false;
        var httpClient = CreateHttpClient(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
        });
        var sut = CreateSut(httpClient, resolvedIp: IPAddress.Loopback);

        var (localPath, _) = await sut.DownloadAsync("http://localhost/foto.jpg");

        localPath.Should().BeNull();
        httpCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAsync_RetornaNull_QuandoHostResolveParaIpPrivado()
    {
        // Host publico ("cdn.example.com") cujo DNS (fake) resolve para um IP privado —
        // cobre o caminho de resolucao via _hostResolver, nao apenas IP literal na URL.
        var httpCalled = false;
        var httpClient = CreateHttpClient(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
        });
        var sut = CreateSut(httpClient, resolvedIp: IPAddress.Parse("127.0.0.1"));

        var (localPath, _) = await sut.DownloadAsync("https://cdn.example.com/foto.jpg");

        localPath.Should().BeNull();
        httpCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAsync_RetornaNull_QuandoSchemeNaoEhHttpOuHttps()
    {
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1 })
        });
        var sut = CreateSut(httpClient);

        var (localPath, _) = await sut.DownloadAsync("ftp://cdn.example.com/foto.jpg");

        localPath.Should().BeNull();
    }

    [Theory]
    [InlineData("http://[::ffff:169.254.169.254]/latest/meta-data/")]
    [InlineData("http://[::ffff:10.0.0.1]/foto.jpg")]
    public async Task DownloadAsync_RetornaNull_QuandoUrlApontaParaIpv4MapeadoParaIpv6Privado(string url)
    {
        // Regressao (code-review do PR #151): "::ffff:169.254.169.254" e "::ffff:10.0.0.1" tem
        // AddressFamily.InterNetworkV6 mas embrulham um endereco IPv4 privado/metadata. Antes do
        // fix, IsPublicAddress so checava link-local/site-local/ULA no ramo IPv6 e deixava esses
        // enderecos passarem como publicos — bypass do allowlist SSRF via resposta DNS hostil.
        var httpCalled = false;
        var httpClient = CreateHttpClient(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
        });
        var sut = CreateSut(httpClient);

        var (localPath, _) = await sut.DownloadAsync(url);

        localPath.Should().BeNull();
        httpCalled.Should().BeFalse("um IPv4 mapeado para IPv6 privado/metadata deve ser bloqueado igual ao IPv4 puro");
    }

    [Fact]
    public async Task DownloadAsync_RetornaNull_QuandoHostResolveParaIpv4MapeadoParaIpv6Metadata()
    {
        // Mesmo cenario, mas via resolucao DNS (nao IP literal na URL) — cobre o caminho
        // _hostResolver, que e o vetor real de um DNS hostil retornando esse tipo de endereco.
        var httpCalled = false;
        var httpClient = CreateHttpClient(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
        });
        var sut = CreateSut(httpClient, resolvedIp: IPAddress.Parse("::ffff:169.254.169.254"));

        var (localPath, _) = await sut.DownloadAsync("https://cdn.example.com/foto.jpg");

        localPath.Should().BeNull();
        httpCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAsync_ContinuaFuncionando_QuandoUrlResolveParaIpv6Publico()
    {
        // Regressao: enderecos IPv6 publicos legitimos (nao mapeados de IPv4, nao
        // link-local/site-local/ULA) devem continuar sendo aceitos apos o fix.
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        });
        var sut = CreateSut(httpClient, resolvedIp: IPAddress.Parse("2001:4860:4860::8888"));

        var (localPath, mediaType) = await sut.DownloadAsync("https://cdn.example.com/foto.jpg");

        localPath.Should().NotBeNullOrWhiteSpace();
        mediaType.Should().Be("image");

        File.Delete(localPath!);
    }

    [Fact]
    public async Task DownloadAsync_ContinuaFuncionando_QuandoUrlEhPublica()
    {
        // Regressao: a allowlist nao pode quebrar o caminho feliz existente (host publico
        // resolvendo para IP publico).
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        });
        var sut = CreateSut(httpClient);

        var (localPath, mediaType) = await sut.DownloadAsync("https://cdn.example.com/foto.jpg");

        localPath.Should().NotBeNullOrWhiteSpace();
        mediaType.Should().Be("image");

        File.Delete(localPath!);
    }
}
