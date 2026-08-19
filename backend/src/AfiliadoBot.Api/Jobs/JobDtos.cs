using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Api.Jobs;

/// <summary>
/// Item de GET /api/jobs/last-executions (Issue #227, especificacao-tecnica.md §2). `JobName` e
/// serializado como slug kebab-case (mesmo valor de `JobKind` do frontend); `Status` como
/// "running"/"success"/"failed"/null (nunca executado) — contrato explicito do DTO, nao a
/// convencao PascalCase de `.ToString()` usada por outros enums do projeto (ver `JobNameSlugs`).
/// </summary>
public record JobLastExecutionDto(
    string JobName,
    string? Status,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? ErrorMessage);

/// <summary>
/// Mapeamento explicito `JobName` (C#) -> slug kebab-case consumido pelo frontend (mesmos
/// valores de `JobKind` em dashboard/.../core/services/jobs.service.ts). Mapeamento explicito
/// em vez de `JsonStringEnumConverter` porque enums C# nao aceitam hifen no nome do membro
/// (design.md §2.1).
/// </summary>
public static class JobNameSlugs
{
    private static readonly IReadOnlyDictionary<JobName, string> Slugs = new Dictionary<JobName, string>
    {
        [JobName.Collector] = "collector",
        [JobName.CollectorAmazon] = "collector-amazon",
        [JobName.CollectorMercadoLivre] = "collector-mercadolivre",
        [JobName.CollectorShopee] = "collector-shopee",
        [JobName.Processor] = "processor",
        [JobName.Publisher] = "publisher",
    };

    public static string ToSlug(JobName jobName) => Slugs[jobName];

    public static string ToStatusSlug(JobRunStatus status) => status switch
    {
        JobRunStatus.Running => "running",
        JobRunStatus.Success => "success",
        JobRunStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "JobRunStatus desconhecido."),
    };
}
