using AfiliadoBot.Api.Common;
using AfiliadoBot.Api.Public;
using AfiliadoBot.Api.RateLimiting;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Endpoints publicos (Issue #11 / Sub-D), sem autenticacao (CA-D1/CA-A9), protegidos por rate
/// limit ("public-read", 60 req/min/IP — CA-D11/CA-D12) e CORS restrito a origins explicitas.
/// Nunca serializa a entidade Product diretamente — sempre via PublicDealDto (CA-D2).
/// </summary>
[ApiController]
[Route("api/public/deals")]
[AllowAnonymous]
[EnableRateLimiting(RateLimiterConfigurator.PublicReadPolicy)]
public class PublicController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;

    public PublicController(AfiliadoBotDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PublicDealDto>>> GetDeals(
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var query = _db.Products
            .Where(p => p.Status == ProductStatus.Published)
            .OrderByDescending(p => p.AiScore);

        return Ok(await ToDtoPagedResultAsync(query, page, pageSize, ct));
    }

    [HttpGet("category/{categoria}")]
    public async Task<ActionResult<PagedResult<PublicDealDto>>> GetByCategory(
        string categoria, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var query = _db.Products
            .Where(p => p.Status == ProductStatus.Published && p.Category == categoria)
            .OrderByDescending(p => p.AiScore);

        return Ok(await ToDtoPagedResultAsync(query, page, pageSize, ct));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<PublicDealDto>> GetBySlug(string slug, CancellationToken ct)
    {
        var product = await _db.Products
            .Where(p => p.Status == ProductStatus.Published && p.Slug == slug)
            .FirstOrDefaultAsync(ct);

        if (product is null)
            return NotFound();

        return Ok(PublicDealDto.FromProduct(product, Request));
    }

    private async Task<PagedResult<PublicDealDto>> ToDtoPagedResultAsync(
        IQueryable<Domain.Entities.Product> query, int? page, int? pageSize, CancellationToken ct)
    {
        var paged = await query.ToPagedResultAsync(page, pageSize, ct);
        var items = paged.Items.Select(p => PublicDealDto.FromProduct(p, Request)).ToList();

        return new PagedResult<PublicDealDto>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalItems = paged.TotalItems,
        };
    }
}
