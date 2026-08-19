using AfiliadoBot.Application.Jobs;
using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Tests.TestHelpers;

/// <summary>
/// Fake de <see cref="IJobRunTracker"/> para testes de unidade de CollectorJob/ProcessorJob/
/// PublisherJob que nao precisam validar a persistencia do JobRun em si (isso e coberto por
/// JobRunTrackerTests e pelos testes de integracao de JobsController) — apenas invoca a acao
/// diretamente, preservando o comportamento exato do corpo original de ExecuteAsync sem exigir
/// um DbContext extra em cada teste existente.
/// </summary>
public class PassThroughJobRunTracker : IJobRunTracker
{
    public Task RunAsync(JobName jobName, Func<CancellationToken, Task> action, CancellationToken ct)
        => action(ct);
}
