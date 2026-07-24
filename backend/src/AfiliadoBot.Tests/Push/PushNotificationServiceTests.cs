using System.Net;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Push;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WebPush;
using AppSetting = AfiliadoBot.Domain.Entities.AppSetting;
using Product = AfiliadoBot.Domain.Entities.Product;
using PushSubscription = AfiliadoBot.Domain.Entities.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace AfiliadoBot.Tests.Push;

/// <summary>
/// Testes unitarios de <see cref="PushNotificationService"/> (Issue #14 / Sub-A #116),
/// mockando <see cref="IWebPushSender"/> (abstracao fina sobre WebPushClient, especificacao-
/// tecnica.md §3/§8) para nao depender de envio HTTP real.
/// </summary>
public class PushNotificationServiceTests
{
    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static async Task SeedVapidKeysAsync(AfiliadoBotDbContext db, string publicKey = "public-key", string privateKey = "private-key")
    {
        db.AppSettings.Add(new AppSetting("push.vapid_public_key", publicKey));
        db.AppSettings.Add(new AppSetting("push.vapid_private_key", privateKey));
        await db.SaveChangesAsync();
    }

    private static Product CriarProduto(string? mediaUrl = "https://cdn.teste/img.png", string? mediaLocalPath = null)
    {
        var product = new Product(
            title: "Produto Teste",
            description: "Descricao",
            salePrice: 99.9m,
            originalPrice: 150m,
            discountPct: 33m,
            affiliateLink: "https://link.teste",
            slug: "produto-teste",
            category: "Geral",
            platform: Platform.Amazon,
            mediaUrl: mediaUrl);

        if (mediaLocalPath is not null)
            product.SetLocalMedia(mediaLocalPath, "image/png");

        return product;
    }

    [Fact]
    public async Task SendIndividualAsync_SemVapidKeys_NaoEnviaNadaENaoFalha()
    {
        using var db = CreateInMemoryContext();
        var sender = new Mock<IWebPushSender>();
        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);

        var act = async () => await service.SendIndividualAsync(CriarProduto());

        await act.Should().NotThrowAsync();
        sender.Verify(s => s.SendAsync(
            It.IsAny<WebPushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendIndividualAsync_SemSubscriptionsCadastradas_NaoEnviaNada()
    {
        using var db = CreateInMemoryContext();
        await SeedVapidKeysAsync(db);
        var sender = new Mock<IWebPushSender>();
        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);

        await service.SendIndividualAsync(CriarProduto());

        sender.Verify(s => s.SendAsync(
            It.IsAny<WebPushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendIndividualAsync_ComSubscriptionsCadastradas_EnviaParaTodas()
    {
        using var db = CreateInMemoryContext();
        await SeedVapidKeysAsync(db);
        db.PushSubscriptions.Add(new PushSubscription("https://endpoint1", "p1", "a1"));
        db.PushSubscriptions.Add(new PushSubscription("https://endpoint2", "p2", "a2"));
        await db.SaveChangesAsync();

        var sender = new Mock<IWebPushSender>();
        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);

        await service.SendIndividualAsync(CriarProduto());

        sender.Verify(s => s.SendAsync(
            It.IsAny<WebPushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SendIndividualAsync_PayloadContemTituloCorpoEImagemDoProduto()
    {
        using var db = CreateInMemoryContext();
        await SeedVapidKeysAsync(db);
        db.PushSubscriptions.Add(new PushSubscription("https://endpoint1", "p1", "a1"));
        await db.SaveChangesAsync();

        string? payloadCapturado = null;
        var sender = new Mock<IWebPushSender>();
        sender.Setup(s => s.SendAsync(
                It.IsAny<WebPushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
            .Callback<WebPushSubscription, string, VapidDetails, CancellationToken>((_, payload, _, _) => payloadCapturado = payload)
            .Returns(Task.CompletedTask);

        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);
        var product = CriarProduto(mediaUrl: "https://cdn.teste/media.png");

        await service.SendIndividualAsync(product);

        payloadCapturado.Should().NotBeNull();
        payloadCapturado.Should().Contain("Nova oferta do Mulet");
        payloadCapturado.Should().Contain("Produto Teste");
        payloadCapturado.Should().Contain("99.90");
        payloadCapturado.Should().Contain("33% OFF");
        payloadCapturado.Should().Contain("https://cdn.teste/media.png");
        payloadCapturado.Should().Contain("produto-teste");
    }

    [Fact]
    public async Task SendIndividualAsync_SemMediaUrl_UsaMediaLocalPathComoImagem()
    {
        using var db = CreateInMemoryContext();
        await SeedVapidKeysAsync(db);
        db.PushSubscriptions.Add(new PushSubscription("https://endpoint1", "p1", "a1"));
        await db.SaveChangesAsync();

        string? payloadCapturado = null;
        var sender = new Mock<IWebPushSender>();
        sender.Setup(s => s.SendAsync(
                It.IsAny<WebPushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
            .Callback<WebPushSubscription, string, VapidDetails, CancellationToken>((_, payload, _, _) => payloadCapturado = payload)
            .Returns(Task.CompletedTask);

        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);
        var product = CriarProduto(mediaUrl: null, mediaLocalPath: "/app/media/local.png");

        await service.SendIndividualAsync(product);

        payloadCapturado.Should().Contain("/app/media/local.png");
    }

    [Fact]
    public async Task SendConsolidatedAsync_PayloadContemContagemENaoContemImage()
    {
        using var db = CreateInMemoryContext();
        await SeedVapidKeysAsync(db);
        db.PushSubscriptions.Add(new PushSubscription("https://endpoint1", "p1", "a1"));
        await db.SaveChangesAsync();

        string? payloadCapturado = null;
        var sender = new Mock<IWebPushSender>();
        sender.Setup(s => s.SendAsync(
                It.IsAny<WebPushSubscription>(), It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
            .Callback<WebPushSubscription, string, VapidDetails, CancellationToken>((_, payload, _, _) => payloadCapturado = payload)
            .Returns(Task.CompletedTask);

        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);

        await service.SendConsolidatedAsync(3);

        payloadCapturado.Should().Contain("3 novas ofertas hoje");
        payloadCapturado.Should().NotContain("\"image\"");
    }

    [Fact]
    public async Task SendToAll_SubscriptionRetorna410Gone_RemoveDoBancoENaoInterrompeODemais()
    {
        using var db = CreateInMemoryContext();
        await SeedVapidKeysAsync(db);
        var subscriptionGone = new PushSubscription("https://endpoint-gone", "p1", "a1");
        var subscriptionOk = new PushSubscription("https://endpoint-ok", "p2", "a2");
        db.PushSubscriptions.Add(subscriptionGone);
        db.PushSubscriptions.Add(subscriptionOk);
        await db.SaveChangesAsync();

        var goneResponse = new HttpResponseMessage(HttpStatusCode.Gone);
        var sender = new Mock<IWebPushSender>();
        sender.Setup(s => s.SendAsync(
                It.Is<WebPushSubscription>(sub => sub.Endpoint == "https://endpoint-gone"),
                It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebPushException("Gone", new WebPushSubscription("https://endpoint-gone", "p1", "a1"), goneResponse));
        sender.Setup(s => s.SendAsync(
                It.Is<WebPushSubscription>(sub => sub.Endpoint == "https://endpoint-ok"),
                It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);

        await service.SendConsolidatedAsync(2);

        (await db.PushSubscriptions.AnyAsync(s => s.Endpoint == "https://endpoint-gone")).Should().BeFalse();
        (await db.PushSubscriptions.AnyAsync(s => s.Endpoint == "https://endpoint-ok")).Should().BeTrue();
        sender.Verify(s => s.SendAsync(
            It.Is<WebPushSubscription>(sub => sub.Endpoint == "https://endpoint-ok"),
            It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendToAll_FalhaGenericaEmUmaSubscription_NaoInterrompeEnvioAsDemais()
    {
        using var db = CreateInMemoryContext();
        await SeedVapidKeysAsync(db);
        var subscriptionFalha = new PushSubscription("https://endpoint-falha", "p1", "a1");
        var subscriptionOk = new PushSubscription("https://endpoint-ok", "p2", "a2");
        db.PushSubscriptions.Add(subscriptionFalha);
        db.PushSubscriptions.Add(subscriptionOk);
        await db.SaveChangesAsync();

        var sender = new Mock<IWebPushSender>();
        sender.Setup(s => s.SendAsync(
                It.Is<WebPushSubscription>(sub => sub.Endpoint == "https://endpoint-falha"),
                It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("erro de rede simulado"));
        sender.Setup(s => s.SendAsync(
                It.Is<WebPushSubscription>(sub => sub.Endpoint == "https://endpoint-ok"),
                It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new PushNotificationService(db, sender.Object, NullLogger<PushNotificationService>.Instance);

        var act = async () => await service.SendConsolidatedAsync(2);

        await act.Should().NotThrowAsync();
        // Falha generica NAO remove a subscription (so 410 Gone remove).
        (await db.PushSubscriptions.AnyAsync(s => s.Endpoint == "https://endpoint-falha")).Should().BeTrue();
        sender.Verify(s => s.SendAsync(
            It.Is<WebPushSubscription>(sub => sub.Endpoint == "https://endpoint-ok"),
            It.IsAny<string>(), It.IsAny<VapidDetails>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
