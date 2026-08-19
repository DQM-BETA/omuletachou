namespace AfiliadoBot.Domain.Enums;

/// <summary>
/// Identifica cada um dos 6 "jobs" exibidos na tela Jobs do dashboard (Issue #227).
/// Granularidade 1:1 com o `JobKind` ja existente no frontend
/// (dashboard/.../core/services/jobs.service.ts) — nao e a granularidade de job Hangfire
/// (que so tem 2 recorrentes: collector-job/publisher-job). Ver design.md §2.1.
/// </summary>
public enum JobName
{
    Collector,
    CollectorAmazon,
    CollectorMercadoLivre,
    CollectorShopee,
    Processor,
    Publisher,
}
