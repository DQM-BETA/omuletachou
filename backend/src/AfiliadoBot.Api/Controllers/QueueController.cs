using AfiliadoBot.Api.Common;
using AfiliadoBot.Api.Queue;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Listagem da fila de publicacao do dashboard (Issue #11 / Sub-B). Protegido por [Authorize]
/// (CA-B11).
/// </summary>
[ApiController]
[Route("api/queue")]
[Authorize]
public class QueueController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;

    public QueueController(AfiliadoBotDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// CA-B7: paginacao + filtros opcionais status/network. Filtro invalido (fora do enum) nao
    /// gera 400 — apenas nao retorna itens, mantendo page/pageSize normalizados no envelope.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<QueueItemDto>>> GetQueue(
        [FromQuery] string? status,
        [FromQuery] string? network,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var query = _db.PublicationQueues.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = Enum.TryParse<PublicationStatus>(status, ignoreCase: true, out var statusFilter)
                ? query.Where(q => q.Status == statusFilter)
                : query.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(network))
        {
            query = Enum.TryParse<SocialNetwork>(network, ignoreCase: true, out var networkFilter)
                ? query.Where(q => q.SocialNetwork == networkFilter)
                : query.Where(_ => false);
        }

        var result = await query
            .OrderBy(q => q.ScheduledAt)
            .Select(q => new QueueItemDto(
                q.Id,
                q.ProductId,
                q.SocialNetwork.ToString(),
                q.Status.ToString(),
                q.ScheduledAt,
                q.PublishedAt,
                q.RetryCount,
                q.ErrorMessage,
                q.CreatedAt))
            .ToPagedResultAsync(page, pageSize, ct);

        return Ok(result);
    }
}
