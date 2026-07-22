using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Metricas agregadas do dashboard (Issue #11 / Sub-E). Protegido por [Authorize]
/// (CA-E5/CA-E6) — diferente de PushController, que e publico.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;

    public ReportsController(AfiliadoBotDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// CA-E5: total de publicacoes com sucesso nos ultimos 7 dias, agrupado por rede
    /// social e por dia (janela [hoje-6, hoje], UTC).
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var periodStart = today.AddDays(-6);
        var periodEndExclusive = today.AddDays(1);

        var published = await _db.PublicationQueues
            .Where(q => q.Status == PublicationStatus.Published
                        && q.PublishedAt != null
                        && q.PublishedAt >= periodStart
                        && q.PublishedAt < periodEndExclusive)
            .Select(q => new { q.SocialNetwork, PublishedAt = q.PublishedAt!.Value })
            .ToListAsync(ct);

        var byNetwork = published
            .GroupBy(p => p.SocialNetwork)
            .Select(g => new { network = g.Key.ToString(), count = g.Count() })
            .OrderBy(g => g.network)
            .ToList();

        var byDay = published
            .GroupBy(p => p.PublishedAt.Date)
            .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Count() })
            .OrderBy(g => g.date)
            .ToList();

        return Ok(new
        {
            periodStart = periodStart.ToString("yyyy-MM-dd"),
            periodEnd = today.ToString("yyyy-MM-dd"),
            totalPublished = published.Count,
            byNetwork,
            byDay,
        });
    }

    /// <summary>
    /// Issue #13 / Sub-D, #106: totais agregados de publicacoes hoje/semana/mes (cards da tela
    /// Reports, CA-D4) — janela distinta do resumo de 7 dias de <see cref="Summary"/> (usado no
    /// grafico, CA-D5). "week" comeca na segunda-feira ISO corrente (UTC); "month" comeca no
    /// primeiro dia do mes corrente (UTC).
    /// </summary>
    [HttpGet("totals")]
    public async Task<IActionResult> Totals(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Segunda-feira ISO da semana corrente (DayOfWeek: Sunday=0 .. Saturday=6).
        var diffToMonday = ((int)today.DayOfWeek + 6) % 7;
        var weekStart = today.AddDays(-diffToMonday);

        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var tomorrow = today.AddDays(1);

        var basePublished = _db.PublicationQueues
            .Where(q => q.Status == PublicationStatus.Published && q.PublishedAt != null);

        var todayCount = await basePublished.CountAsync(q => q.PublishedAt >= today && q.PublishedAt < tomorrow, ct);
        var weekCount = await basePublished.CountAsync(q => q.PublishedAt >= weekStart, ct);
        var monthCount = await basePublished.CountAsync(q => q.PublishedAt >= monthStart, ct);

        return Ok(new
        {
            today = todayCount,
            week = weekCount,
            month = monthCount,
        });
    }
}
