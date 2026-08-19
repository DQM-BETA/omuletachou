using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Entities;

/// <summary>
/// Registro de uma execucao de job (Issue #227): histórico proprio, desacoplado do Hangfire,
/// alimentado por IJobRunTracker (AfiliadoBot.Application.Jobs) — cobre tanto o disparo agendado
/// (cron/enqueue) quanto o disparo manual (JobsController), ja que ambos convergem para o
/// mesmo metodo instrumentado (design.md §2.2). Segue o mesmo padrao de
/// <see cref="PublicationQueue"/>/<see cref="PublicationLog"/>: construtor privado para EF Core,
/// setters privados, metodos de dominio para transicao de estado.
/// </summary>
public class JobRun
{
    public Guid Id { get; private set; }
    public JobName JobName { get; private set; }
    public JobRunStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Construtor para EF Core
    private JobRun() { }

    public static JobRun Start(JobName jobName) => new()
    {
        Id = Guid.NewGuid(),
        JobName = jobName,
        Status = JobRunStatus.Running,
        StartedAt = DateTime.UtcNow,
    };

    public void MarkAsSuccess()
    {
        Status = JobRunStatus.Success;
        FinishedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string? errorMessage)
    {
        Status = JobRunStatus.Failed;
        FinishedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }
}
