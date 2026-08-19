using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Tests.Jobs;

/// <summary>
/// Cobre CA 4.1/5.1 (design.md §2.2, especificacao-tecnica.md §3): RunAsync persiste Running
/// antes de executar a acao, Success/Failed ao final, e sempre relanca a excecao.
/// </summary>
public class JobRunTrackerTests
{
    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    [Fact]
    public async Task RunAsync_PersisteRunning_AntesDeExecutarAAcao()
    {
        using var db = CreateInMemoryContext();
        var tracker = new JobRunTracker(db);

        var statusNoMomentoDaAcao = (JobRunStatus?)null;

        await tracker.RunAsync(JobName.Collector, async ct =>
        {
            var run = await db.JobRuns.AsNoTracking().SingleAsync(ct);
            statusNoMomentoDaAcao = run.Status;
        }, CancellationToken.None);

        statusNoMomentoDaAcao.Should().Be(JobRunStatus.Running);
    }

    [Fact]
    public async Task RunAsync_MarcaSuccess_EFinishedAt_AposSucesso()
    {
        using var db = CreateInMemoryContext();
        var tracker = new JobRunTracker(db);

        await tracker.RunAsync(JobName.Processor, _ => Task.CompletedTask, CancellationToken.None);

        var run = await db.JobRuns.AsNoTracking().SingleAsync();
        run.JobName.Should().Be(JobName.Processor);
        run.Status.Should().Be(JobRunStatus.Success);
        run.FinishedAt.Should().NotBeNull();
        run.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_MarcaFailed_ComErrorMessageEFinishedAt_AposExcecao_ERelanca()
    {
        using var db = CreateInMemoryContext();
        var tracker = new JobRunTracker(db);

        var act = async () => await tracker.RunAsync(
            JobName.Publisher,
            _ => throw new InvalidOperationException("falha simulada"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("falha simulada");

        var run = await db.JobRuns.AsNoTracking().SingleAsync();
        run.Status.Should().Be(JobRunStatus.Failed);
        run.FinishedAt.Should().NotBeNull();
        run.ErrorMessage.Should().Be("falha simulada");
    }

    [Fact]
    public async Task RunAsync_MarcaFailed_QuandoOperationCanceledException_ERelanca()
    {
        // design.md §2.2: "inclui OperationCanceledException — nunca deixa o run preso em
        // Running". A persistencia final usa CancellationToken.None (nao herda o ct cancelado).
        using var db = CreateInMemoryContext();
        var tracker = new JobRunTracker(db);
        using var cts = new CancellationTokenSource();

        var act = async () => await tracker.RunAsync(
            JobName.CollectorAmazon,
            ct =>
            {
                cts.Cancel();
                throw new OperationCanceledException();
            },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        var run = await db.JobRuns.AsNoTracking().SingleAsync();
        run.Status.Should().Be(JobRunStatus.Failed);
        run.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_PersisteJobNameCorreto()
    {
        using var db = CreateInMemoryContext();
        var tracker = new JobRunTracker(db);

        await tracker.RunAsync(JobName.CollectorShopee, _ => Task.CompletedTask, CancellationToken.None);

        var run = await db.JobRuns.AsNoTracking().SingleAsync();
        run.JobName.Should().Be(JobName.CollectorShopee);
    }

    [Fact]
    public async Task RunAsync_StartedAt_EPreenchidoAntesDaAcaoRodar()
    {
        using var db = CreateInMemoryContext();
        var tracker = new JobRunTracker(db);
        var before = DateTime.UtcNow;

        await tracker.RunAsync(JobName.CollectorMercadoLivre, _ => Task.CompletedTask, CancellationToken.None);

        var run = await db.JobRuns.AsNoTracking().SingleAsync();
        run.StartedAt.Should().BeOnOrAfter(before);
        run.StartedAt.Should().BeOnOrBefore(run.FinishedAt!.Value);
    }
}
