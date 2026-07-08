using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AfiliadoBot.Tests.Jobs;

public class PublisherJobTests
{
    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static Product CriarProduto(string title = "Produto Teste")
    {
        var product = new Product(
            title: title,
            description: "Descricao",
            salePrice: 100m,
            originalPrice: 120m,
            discountPct: 10m,
            affiliateLink: "https://link.teste",
            slug: Guid.NewGuid().ToString(),
            category: "Geral",
            platform: Platform.Amazon);

        product.UpdateAiResult(8, "Bom desconto", "Legenda de teste");
        return product;
    }

    private static Mock<ISocialPublisher> CreatePublisherMock(SocialNetwork network, bool success = true)
    {
        var mock = new Mock<ISocialPublisher>();
        mock.SetupGet(p => p.Network).Returns(network);
        mock.Setup(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);
        return mock;
    }

    private static PublisherJob CreateJob(AfiliadoBotDbContext db, IEnumerable<ISocialPublisher> publishers)
        => new(db, publishers, NullLogger<PublisherJob>.Instance);

    [Fact]
    public async Task ExecuteAsync_PublicaItensScheduledVencidos()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddMinutes(-5));
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var telegram = CreatePublisherMock(SocialNetwork.Telegram);
        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        telegram.Verify(p => p.PublishAsync(It.Is<PublicationQueue>(i => i.Id == item.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NaoPublicaItensScheduledFuturos()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(2));
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var telegram = CreatePublisherMock(SocialNetwork.Telegram);
        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        telegram.Verify(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReprocessaItensFailedComCanRetryTrue()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(-1));
        item.RegisterAttempt(false, "erro anterior"); // RetryCount = 1, Status = Failed, CanRetry = true
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var telegram = CreatePublisherMock(SocialNetwork.Telegram);
        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        telegram.Verify(p => p.PublishAsync(It.Is<PublicationQueue>(i => i.Id == item.Id), It.IsAny<CancellationToken>()), Times.Once);

        var updated = await db.PublicationQueues.FirstAsync(i => i.Id == item.Id);
        updated.RetryCount.Should().Be(1); // sucesso nao incrementa
        updated.Status.Should().Be(PublicationStatus.Published);
    }

    [Fact]
    public async Task ExecuteAsync_NaoProcessaItensFailedComCanRetryFalse()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(-1));
        item.RegisterAttempt(false, "erro 1");
        item.RegisterAttempt(false, "erro 2");
        item.RegisterAttempt(false, "erro 3"); // RetryCount = 3, CanRetry = false
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var telegram = CreatePublisherMock(SocialNetwork.Telegram);
        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        telegram.Verify(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoraItensManualPending()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Facebook, DateTime.UtcNow.AddHours(-1));
        item.MarkAsManualPending();
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var facebook = CreatePublisherMock(SocialNetwork.Facebook);
        var job = CreateJob(db, new[] { facebook.Object });

        await job.ExecuteAsync();

        facebook.Verify(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OrdenaPorScheduledAtAscCreatedAtAsc()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);

        var baseTime = DateTime.UtcNow.AddHours(-1);
        var itemB = new PublicationQueue(product.Id, SocialNetwork.Telegram, baseTime.AddMinutes(10));
        var itemA = new PublicationQueue(product.Id, SocialNetwork.Telegram, baseTime);
        var itemC = new PublicationQueue(product.Id, SocialNetwork.Telegram, baseTime.AddMinutes(20));

        db.PublicationQueues.AddRange(itemB, itemA, itemC);
        await db.SaveChangesAsync();

        var callOrder = new List<Guid>();
        var telegram = new Mock<ISocialPublisher>();
        telegram.SetupGet(p => p.Network).Returns(SocialNetwork.Telegram);
        telegram.Setup(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()))
            .Callback<PublicationQueue, CancellationToken>((i, _) => callOrder.Add(i.Id))
            .ReturnsAsync(true);

        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        callOrder.Should().Equal(itemA.Id, itemB.Id, itemC.Id);
    }

    [Fact]
    public async Task ExecuteAsync_MarcaPublished_QuandoSucesso()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(-1));
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var telegram = CreatePublisherMock(SocialNetwork.Telegram, success: true);
        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        var updated = await db.PublicationQueues.FirstAsync(i => i.Id == item.Id);
        updated.Status.Should().Be(PublicationStatus.Published);
        updated.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_IncrementaRetryCount_QuandoFalha()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(-1));
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var telegram = new Mock<ISocialPublisher>();
        telegram.SetupGet(p => p.Network).Returns(SocialNetwork.Telegram);
        telegram.Setup(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falha simulada"));

        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        var updated = await db.PublicationQueues.FirstAsync(i => i.Id == item.Id);
        updated.RetryCount.Should().Be(1);
        updated.Status.Should().Be(PublicationStatus.Failed);
        updated.ErrorMessage.Should().Be("falha simulada");
        updated.CanRetry.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SetaCanRetryFalse_QuandoRetryCountAtinge3()
    {
        using var db = CreateInMemoryContext();
        var product = CriarProduto();
        db.Products.Add(product);
        var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(-1));
        item.RegisterAttempt(false, "erro 1");
        item.RegisterAttempt(false, "erro 2"); // RetryCount = 2, ainda CanRetry
        db.PublicationQueues.Add(item);
        await db.SaveChangesAsync();

        var telegram = new Mock<ISocialPublisher>();
        telegram.SetupGet(p => p.Network).Returns(SocialNetwork.Telegram);
        telegram.Setup(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falha final"));

        var job = CreateJob(db, new[] { telegram.Object });

        await job.ExecuteAsync();

        var updated = await db.PublicationQueues.FirstAsync(i => i.Id == item.Id);
        updated.RetryCount.Should().Be(3);
        updated.Status.Should().Be(PublicationStatus.Failed);
        updated.CanRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_NenhumItemPendente_NaoFalha()
    {
        using var db = CreateInMemoryContext();
        var telegram = CreatePublisherMock(SocialNetwork.Telegram);
        var job = CreateJob(db, new[] { telegram.Object });

        var act = async () => await job.ExecuteAsync();

        await act.Should().NotThrowAsync();
        telegram.Verify(p => p.PublishAsync(It.IsAny<PublicationQueue>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
