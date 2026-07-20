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
}
