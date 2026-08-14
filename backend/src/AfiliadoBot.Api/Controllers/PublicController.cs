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
/// Issue #167 (design.md secao 5.2): a antiga rota <c>GET api/public/deals/category/{categoria}</c>
/// foi REMOVIDA — <c>GetDeals</c> abaixo cobre o mesmo caso via <c>?category=</c>, com filtros
/// adicionais. Decisao final de remocao (nao manter compatibilidade temporaria) documentada no
/// PR desta sub-issue.
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

    /// <summary>
    /// Lista ofertas publicadas, com filtros combinaveis e opcionais (Issue #167, CA 6.1-6.6).
    /// Sem nenhum parametro, comportamento identico ao endpoint anterior a Issue #167 (CA 6.1):
    /// mesma paginacao, ordenacao padrao por AiScore desc (CA 6.5). Filtro/sort nao reconhecido
    /// nunca retorna erro — category/subcategory inexistentes resultam em lista vazia (CA 6.6);
    /// sort desconhecido cai no default de AiScore (mesma postura defensiva).
    /// A ordem dos filtros abaixo (category/subcategory como prefixo de igualdade, depois os
    /// filtros de range de preco/desconto, sort por ultimo) segue a ordem das colunas dos 5
    /// indices compostos criados na Issue #168 (design.md secao 4.2), para o planner do Postgres
    /// poder aproveitar o indice composto correto por combinacao de filtro+sort.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PublicDealDto>>> GetDeals(
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? category, [FromQuery] string? subcategory,
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? minDiscount, [FromQuery] string? sort,
        CancellationToken ct)
    {
        var query = _db.Products.Where(p => p.Status == ProductStatus.Published);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(subcategory))
            query = query.Where(p => p.Subcategory == subcategory);
        if (minPrice.HasValue)
            query = query.Where(p => p.SalePrice >= minPrice.Value);
        if (maxPrice.HasValue)
            query = query.Where(p => p.SalePrice <= maxPrice.Value);
        if (minDiscount.HasValue)
            query = query.Where(p => p.DiscountPct >= minDiscount.Value);

        var ordered = sort switch
        {
            "price_asc" => query.OrderBy(p => p.SalePrice),
            "discount_desc" => query.OrderByDescending(p => p.DiscountPct),
            "recent" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.AiScore), // default (CA 6.5) — cobre "relevance" e qualquer valor nao reconhecido
        };

        return Ok(await ToDtoPagedResultAsync(ordered, page, pageSize, ct));
    }

    /// <summary>
    /// Arvore Category -> [Subcategory] com contagem de produtos Published ativos em cada
    /// (Issue #167, CA 6.7). Rota absoluta ("~/") porque foge do prefixo "api/public/deals" da
    /// classe — decisao registrada em especificacao-tecnica.md secao 8 (alternativa a criar um
    /// controller dedicado; optei por manter no mesmo controller por ser o unico outro endpoint
    /// publico "de listagem", sem justificar um controller novo so para 1 rota).
    /// </summary>
    [HttpGet("~/api/public/categories")]
    public async Task<ActionResult<List<CategoryTreeDto>>> GetCategories(CancellationToken ct)
    {
        var grouped = await _db.Products
            .Where(p => p.Status == ProductStatus.Published)
            .GroupBy(p => new { p.Category, p.Subcategory })
            .Select(g => new { g.Key.Category, g.Key.Subcategory, Count = g.Count() })
            .ToListAsync(ct);

        var tree = grouped
            .GroupBy(x => x.Category)
            .Select(g => new CategoryTreeDto
            {
                Category = g.Key,
                Subcategories = g.Where(x => x.Subcategory != null)
                    .Select(x => new SubcategoryCountDto { Subcategory = x.Subcategory!, Count = x.Count })
                    .ToList(),
                Count = g.Sum(x => x.Count),
            })
            .ToList();

        return Ok(tree);
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
