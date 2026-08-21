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
/// Endpoints publicos por produto (Issue #231), separado de <see cref="PublicController"/>
/// (recurso "deal", por slug/filtro) — os endpoints aqui giram em torno do recurso "product" por
/// id (design.md secao 12.1 / especificacao-tecnica.md §3.1). Sem autenticacao (evento anonimo /
/// leitura publica), sem rate limit de classe (policies diferentes por metodo — ver 3.2/3.3 da
/// especificacao-tecnica.md).
/// </summary>
[ApiController]
[Route("api/public/products")]
[AllowAnonymous]
public class PublicProductsController : ControllerBase
{
    private const int SuggestedLimit = 10;
    private const int SuggestedMinimumToShow = 4;

    private readonly AfiliadoBotDbContext _db;

    public PublicProductsController(AfiliadoBotDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Faixa de produtos sugeridos (Issue #231, sub-issue #278, T-03). Fallback calculado no
    /// backend (design.md §6, especificacao-tecnica.md §3.3/§6): quando <paramref name="categories"/>
    /// esta vazio/ausente OU <paramref name="hasResults"/> e <c>false</c>, ignora o filtro de
    /// categoria e devolve o ranking geral (mais clicados entre todas as categorias). Caso
    /// contrario, restringe as categorias informadas (CSV) e ordena o ranking dentro delas.
    /// Ordenacao sempre por <c>ClickCount</c> desc, desempate por <c>CreatedAt</c> desc (garante
    /// que a faixa nao fica vazia so por falta de historico de cliques ainda — CA 1.7), limitada a
    /// <see cref="SuggestedLimit"/> itens. Corte minimo de <see cref="SuggestedMinimumToShow"/>:
    /// abaixo disso devolve lista vazia (nao erro) — o frontend decide nao renderizar (CA 1.5).
    /// </summary>
    [HttpGet("suggested")]
    [EnableRateLimiting(RateLimiterConfigurator.PublicReadPolicy)]
    public async Task<ActionResult<List<PublicDealDto>>> GetSuggested(
        [FromQuery] string? categories, [FromQuery] bool hasResults, CancellationToken ct)
    {
        var categoryList = string.IsNullOrWhiteSpace(categories)
            ? Array.Empty<string>()
            : categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var query = _db.Products.Where(p => p.Status == ProductStatus.Published);

        query = (categoryList.Length == 0 || !hasResults)
            ? query
            : query.Where(p => categoryList.Contains(p.Category));

        var products = await query
            .OrderByDescending(p => p.ClickCount)
            .ThenByDescending(p => p.CreatedAt)
            .Take(SuggestedLimit)
            .ToListAsync(ct);

        if (products.Count < SuggestedMinimumToShow)
            return Ok(new List<PublicDealDto>());

        return Ok(products.Select(p => PublicDealDto.FromProduct(p, Request)).ToList());
    }
}
