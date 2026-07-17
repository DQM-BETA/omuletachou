using AfiliadoBot.Api.Common;
using AfiliadoBot.Api.Products;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Listagem/detalhe de produtos do dashboard (Issue #11 / Sub-B). Protegido por [Authorize]
/// (CA-B11) — o middleware de Sub-A (#81) ja resolve o ClaimsPrincipal a partir do JWT.
/// </summary>
[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;

    public ProductsController(AfiliadoBotDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// CA-B1 (paginacao default), CA-B2 (filtros status/platform). Filtro invalido (fora do
    /// enum) nao gera 400 — apenas nao retorna itens, mantendo page/pageSize normalizados no
    /// envelope de resposta.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetProducts(
        [FromQuery] string? status,
        [FromQuery] string? platform,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = Enum.TryParse<ProductStatus>(status, ignoreCase: true, out var statusFilter)
                ? query.Where(p => p.Status == statusFilter)
                : query.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = Enum.TryParse<Platform>(platform, ignoreCase: true, out var platformFilter)
                ? query.Where(p => p.Platform == platformFilter)
                : query.Where(_ => false);
        }

        var result = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Title,
                p.SalePrice,
                p.OriginalPrice,
                p.DiscountPct,
                p.Status.ToString(),
                p.Platform.ToString(),
                p.Slug,
                p.Category,
                p.CreatedAt))
            .ToPagedResultAsync(page, pageSize, ct);

        return Ok(result);
    }

    /// <summary>CA-B3 (inclui ai_score/ai_reason), CA-B4 (404 quando inexistente).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailDto>> GetProduct(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return NotFound();

        var dto = new ProductDetailDto(
            product.Id,
            product.Title,
            product.Description,
            product.SalePrice,
            product.OriginalPrice,
            product.DiscountPct,
            product.AffiliateLink,
            product.ImageUrl,
            product.MediaUrl,
            product.MediaLocalPath,
            product.Slug,
            product.Category,
            product.Platform.ToString(),
            product.Status.ToString(),
            product.AiScore,
            product.AiReason,
            product.CreatedAt,
            product.UpdatedAt);

        return Ok(dto);
    }
}
