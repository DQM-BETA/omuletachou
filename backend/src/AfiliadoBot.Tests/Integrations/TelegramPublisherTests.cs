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

public class TelegramPublisherTests
{
    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static async Task SeedCredentialsAsync(
        AfiliadoBotDbContext db, string botToken = "123:ABC", string channelId = "@canalteste")
    {
        db.AppSettings.Add(new AppSetting("telegram.bot_token", botToken));
        db.AppSettings.Add(new AppSetting("telegram.channel_id", channelId));
        await db.SaveChangesAsync();
    }

    private static Product CriarProduto(string? mediaLocalPath, string? mediaUrl, string? mediaType)
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

        product.UpdateAiResult(8, "Bom desconto", "Legenda de teste");
        product.SetLocalMedia(mediaLocalPath, mediaType);
        return product;
    }

    private static (HttpClient Client, Func<Uri?> LastRequestUri, Func<string?> LastRequestBody) CreateHttpClient(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        Uri? lastRequestUri = null;
        string? lastRequestBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
            {
                lastRequestUri = req.RequestUri;
                lastRequestBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
                return new HttpResponseMessage(statusCode) { Content = new StringContent("{\"ok\":true}") };
            });

        return (new HttpClient(handlerMock.Object), () => lastRequestUri, () => lastRequestBody);
    }

    [Fact]
    public async Task PublishAsync_EnviaVideo_QuandoMediaTypeVideo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: "arquivo-inexistente.mp4", mediaUrl: null, mediaType: "video");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, lastRequestUri, _) = CreateHttpClient();
        var publisher = new TelegramPublisher(httpClient, db, NullLogger<TelegramPublisher>.Instance);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        lastRequestUri().Should().NotBeNull();
        lastRequestUri()!.ToString().Should().Contain("/sendVideo");
    }

    [Fact]
    public async Task PublishAsync_EnviaFoto_QuandoMediaTypeImage()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: "arquivo-inexistente.jpg", mediaUrl: null, mediaType: "image");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, lastRequestUri, _) = CreateHttpClient();
        var publisher = new TelegramPublisher(httpClient, db, NullLogger<TelegramPublisher>.Instance);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        lastRequestUri()!.ToString().Should().Contain("/sendPhoto");
    }

    [Fact]
    public async Task PublishAsync_EnviaMensagemTexto_QuandoSemMidia()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: null);
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, lastRequestUri, _) = CreateHttpClient();
        var publisher = new TelegramPublisher(httpClient, db, NullLogger<TelegramPublisher>.Instance);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        lastRequestUri()!.ToString().Should().Contain("/sendMessage");
    }

    [Fact]
    public async Task PublishAsync_UsaMediaLocalPath_QuandoPresente()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

        try
        {
            var product = CriarProduto(mediaLocalPath: tempFile, mediaUrl: "https://cdn.teste/img.jpg", mediaType: "image");
            db.Products.Add(product);
            var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow);
            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();

            var (httpClient, lastRequestUri, lastRequestBody) = CreateHttpClient();
            var publisher = new TelegramPublisher(httpClient, db, NullLogger<TelegramPublisher>.Instance);

            var result = await publisher.PublishAsync(item);

            result.Should().BeTrue();
            lastRequestUri()!.ToString().Should().Contain("/sendPhoto");
            lastRequestBody().Should().NotContain("https://cdn.teste/img.jpg");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_UsaFallbackMediaUrl_QuandoMediaLocalPathNulo()
    {
        using var db = CreateInMemoryContext();
        await SeedCredentialsAsync(db);

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: "https://cdn.teste/img.jpg", mediaType: "image");
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, lastRequestUri, lastRequestBody) = CreateHttpClient();
        var publisher = new TelegramPublisher(httpClient, db, NullLogger<TelegramPublisher>.Instance);

        var result = await publisher.PublishAsync(item);

        result.Should().BeTrue();
        lastRequestUri()!.ToString().Should().Contain("/sendPhoto");
        lastRequestBody().Should().Contain("https://cdn.teste/img.jpg");
    }

    [Fact]
    public async Task PublishAsync_LancaException_QuandoCredenciaisAusentes()
    {
        using var db = CreateInMemoryContext();
        // Sem seed de credenciais

        var product = CriarProduto(mediaLocalPath: null, mediaUrl: null, mediaType: null);
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow);
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var (httpClient, _, _) = CreateHttpClient();
        var publisher = new TelegramPublisher(httpClient, db, NullLogger<TelegramPublisher>.Instance);

        var act = async () => await publisher.PublishAsync(item);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
