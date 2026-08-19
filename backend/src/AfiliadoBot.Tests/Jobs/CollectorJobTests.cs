using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Tests.TestHelpers;
using FluentAssertions;
using global::Hangfire;
using global::Hangfire.Common;
using global::Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AfiliadoBot.Tests.Jobs;

public class CollectorJobTests
{
    private static Mock<IPlatformCollector> CreateCollectorMock(Platform platform, int productCount)
    {
        var mock = new Mock<IPlatformCollector>();
        mock.SetupGet(c => c.Platform).Returns(platform);
        mock.Setup(c => c.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, productCount).Select(_ => CreateProduct(platform)));
        return mock;
    }

    private static Mock<IPlatformCollector> CreateFailingCollectorMock(Platform platform)
    {
        var mock = new Mock<IPlatformCollector>();
        mock.SetupGet(c => c.Platform).Returns(platform);
        mock.Setup(c => c.CollectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException($"Falha simulada no collector {platform}"));
        return mock;
    }

    private static Product CreateProduct(Platform platform) => new(
        title: "Produto Teste",
        description: "Descricao",
        salePrice: 100m,
        originalPrice: 120m,
        discountPct: 10m,
        affiliateLink: "https://link.teste",
        slug: Guid.NewGuid().ToString(),
        category: "Geral",
        platform: platform);

    private static CollectorJob CreateJob(
        IEnumerable<IPlatformCollector> collectors,
        Mock<IBackgroundJobClient> backgroundJobClientMock)
    {
        return new CollectorJob(
            collectors,
            backgroundJobClientMock.Object,
            new PassThroughJobRunTracker(),
            NullLogger<CollectorJob>.Instance);
    }

    private static Mock<IBackgroundJobClient> CreateBackgroundJobClientMock()
    {
        var mock = new Mock<IBackgroundJobClient>();
        mock.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(Guid.NewGuid().ToString());
        return mock;
    }

    [Fact]
    public async Task ExecuteAsync_ChamaTodosCollectors_EmSequencia()
    {
        var amazon = CreateCollectorMock(Platform.Amazon, 2);
        var mercadoLivre = CreateCollectorMock(Platform.MercadoLivre, 3);
        var shopee = CreateCollectorMock(Platform.Shopee, 1);
        var backgroundJobClient = CreateBackgroundJobClientMock();

        var job = CreateJob(new[] { amazon.Object, mercadoLivre.Object, shopee.Object }, backgroundJobClient);

        await job.ExecuteAsync();

        amazon.Verify(c => c.CollectAsync(It.IsAny<CancellationToken>()), Times.Once);
        mercadoLivre.Verify(c => c.CollectAsync(It.IsAny<CancellationToken>()), Times.Once);
        shopee.Verify(c => c.CollectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuaAposFalhaDeUmCollector()
    {
        var amazon = CreateFailingCollectorMock(Platform.Amazon);
        var mercadoLivre = CreateCollectorMock(Platform.MercadoLivre, 2);
        var shopee = CreateCollectorMock(Platform.Shopee, 1);
        var backgroundJobClient = CreateBackgroundJobClientMock();

        var job = CreateJob(new[] { amazon.Object, mercadoLivre.Object, shopee.Object }, backgroundJobClient);

        var act = async () => await job.ExecuteAsync();

        await act.Should().NotThrowAsync();
        mercadoLivre.Verify(c => c.CollectAsync(It.IsAny<CancellationToken>()), Times.Once);
        shopee.Verify(c => c.CollectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EnfileiraProcessorJob_QuandoAlgumCollectorTeveSucesso()
    {
        var amazon = CreateFailingCollectorMock(Platform.Amazon);
        var mercadoLivre = CreateCollectorMock(Platform.MercadoLivre, 2);
        var backgroundJobClient = CreateBackgroundJobClientMock();

        var job = CreateJob(new[] { amazon.Object, mercadoLivre.Object }, backgroundJobClient);

        await job.ExecuteAsync();

        backgroundJobClient.Verify(
            c => c.Create(
                It.Is<Job>(j => j.Type == typeof(ProcessorJob) && j.Method.Name == nameof(ProcessorJob.ExecuteAsync)),
                It.IsAny<IState>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NaoEnfileiraProcessorJob_QuandoTodosFalharam()
    {
        var amazon = CreateFailingCollectorMock(Platform.Amazon);
        var mercadoLivre = CreateFailingCollectorMock(Platform.MercadoLivre);
        var shopee = CreateFailingCollectorMock(Platform.Shopee);
        var backgroundJobClient = CreateBackgroundJobClientMock();

        var job = CreateJob(new[] { amazon.Object, mercadoLivre.Object, shopee.Object }, backgroundJobClient);

        await job.ExecuteAsync();

        backgroundJobClient.Verify(
            c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
    }

    // --- Issue #227 (JobRun tracking) ---

    [Fact]
    public async Task ExecuteAsync_GeraJobRun_QuandoChamadoDiretamente()
    {
        // CA 4.2/5.1: ExecuteAsync chamado diretamente (simulando o cron do Hangfire, sem passar
        // pelo controller) tambem gera um JobRun — mesmo mecanismo cobre os dois disparadores.
        using var db = new AfiliadoBotDbContext(
            new DbContextOptionsBuilder<AfiliadoBotDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var tracker = new JobRunTracker(db);

        var amazon = CreateCollectorMock(Platform.Amazon, 1);
        var backgroundJobClient = CreateBackgroundJobClientMock();
        var job = new CollectorJob(new[] { amazon.Object }, backgroundJobClient.Object, tracker, NullLogger<CollectorJob>.Instance);

        await job.ExecuteAsync();

        var run = await db.JobRuns.SingleAsync();
        run.JobName.Should().Be(JobName.Collector);
        run.Status.Should().Be(JobRunStatus.Success);
    }
}
