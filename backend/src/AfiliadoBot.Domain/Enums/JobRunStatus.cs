namespace AfiliadoBot.Domain.Enums;

/// <summary>
/// Estado de uma execucao (<see cref="Entities.JobRun"/>) de um job (Issue #227, design.md §2.1).
/// `Running` e persistido enquanto a execucao esta em curso (sincrona, dentro da propria
/// requisicao HTTP manual ou do worker Hangfire) — suportado no dominio ainda que a exibicao
/// dedicada na tela nao seja obrigatoria (design.md §2.3).
/// </summary>
public enum JobRunStatus
{
    Running,
    Success,
    Failed,
}
