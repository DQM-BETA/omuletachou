using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Application.Jobs;

/// <summary>
/// Servico central de instrumentacao de execucoes de job (Issue #227, design.md §2.2): persiste
/// um <see cref="Domain.Entities.JobRun"/> antes de rodar a acao (Status=Running), marca
/// Success/Failed ao final e sempre relanca qualquer excecao (nunca engole erro) — preserva o
/// comportamento existente de retry do Hangfire e dos try/catch ja presentes nos chamadores.
/// </summary>
public interface IJobRunTracker
{
    Task RunAsync(JobName jobName, Func<CancellationToken, Task> action, CancellationToken ct);
}
