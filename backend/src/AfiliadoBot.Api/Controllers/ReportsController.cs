using System.Globalization;
using AfiliadoBot.Api.Reports;
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

    /// <summary>
    /// Issue #228/T-02: cards agregados da tela Reports (total + quebras por Plataforma/
    /// Categoria/Status/Subcategoria) sobre o universo de produtos filtrado. Todos os filtros sao
    /// opcionais e combinam em AND. Sem filtro de Status, o resultado NAO e restrito a Published —
    /// o default de UX "Published quando vazio" e responsabilidade exclusiva do Angular
    /// (design.md §2.3), nao deste endpoint. Filtro invalido (enum desconhecido) nunca retorna
    /// 400 — apenas nao casa nenhum produto (postura defensiva ja usada em
    /// ProductsController.GetProducts). Sem match: 200 OK, Total = 0 e as 4 listas vazias.
    /// </summary>
    [HttpGet("products/summary")]
    public async Task<ActionResult<ProductsReportSummaryDto>> ProductsSummary(
        [FromQuery] string? category,
        [FromQuery] string? subcategory,
        [FromQuery] string? platform,
        [FromQuery] string? status,
        [FromQuery] string? collectedFrom,
        [FromQuery] string? collectedTo,
        CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrWhiteSpace(subcategory))
            query = query.Where(p => p.Subcategory == subcategory);

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = Enum.TryParse<Platform>(platform, ignoreCase: true, out var platformFilter)
                ? query.Where(p => p.Platform == platformFilter)
                : query.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = Enum.TryParse<ProductStatus>(status, ignoreCase: true, out var statusFilter)
                ? query.Where(p => p.Status == statusFilter)
                : query.Where(_ => false);
        }

        // Janela [from, toExclusive) sobre CreatedAt (data de coleta, design.md §1/§2.4) —
        // "yyyy-MM-dd" tratado como o dia UTC completo, inclusive nos dois limites (CA 2.5).
        // Data invalida/nao parseavel e ignorada silenciosamente (mesma postura defensiva do
        // resto do filtro, nunca 400).
        if (!string.IsNullOrWhiteSpace(collectedFrom) &&
            DateTime.TryParse(collectedFrom, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromParsed))
        {
            var from = DateTime.SpecifyKind(fromParsed.Date, DateTimeKind.Utc);
            query = query.Where(p => p.CreatedAt >= from);
        }

        if (!string.IsNullOrWhiteSpace(collectedTo) &&
            DateTime.TryParse(collectedTo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toParsed))
        {
            var toExclusive = DateTime.SpecifyKind(toParsed.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(p => p.CreatedAt < toExclusive);
        }

        var total = await query.CountAsync(ct);

        var byPlatform = await query
            .GroupBy(p => p.Platform)
            .Select(g => new PlatformCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var byCategory = await query
            .GroupBy(p => p.Category)
            .Select(g => new CategoryCountDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var byStatus = await query
            .GroupBy(p => p.Status)
            .Select(g => new StatusCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var bySubcategory = await query
            .GroupBy(p => p.Subcategory)
            .Select(g => new SubcategoryCountDto(g.Key, g.Count()))
            .ToListAsync(ct);

        return Ok(new ProductsReportSummaryDto(total, byPlatform, byCategory, byStatus, bySubcategory));
    }
}
