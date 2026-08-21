using AfiliadoBot.Api.RateLimiting;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AfiliadoBot.Api.Controllers;

/// <summary>
/// Endpoints publicos por produto (Issue #231), separado de <see cref="PublicController"/>
/// (api/public/deals, recurso "deal"/listagem por filtro) por girar em torno do recurso "product"
/// por id (especificacao-tecnica.md secao 3.1, design.md secao 12.1). Sem autenticacao — mesmo
/// padrao dos demais endpoints publicos do site (CA-D1/CA-A9).
/// </summary>
[ApiController]
[Route("api/public/products")]
[AllowAnonymous]
public class PublicProductsController : ControllerBase
{
    private readonly AfiliadoBotDbContext _db;

    public PublicProductsController(AfiliadoBotDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Registra um clique anonimo em um produto (Issue #231 / sub-issue #277, T-02,
    /// especificacao-tecnica.md secao 3.2, design.md secao 7). Sem corpo de request — pensado para
    /// <c>navigator.sendBeacon(url)</c>, que nao envia payload. Sempre <c>202 Accepted</c>, mesmo
    /// quando o produto nao existe (CA 2.2): o <c>sendBeacon</c> do frontend nunca le a resposta,
    /// entao nao ha motivo para expor um 404 que nenhum client vai consumir. Insere o evento
    /// anonimo (<see cref="ProductClick"/>: apenas produto + timestamp, CA 2.3) e incrementa
    /// <c>Product.ClickCount</c> (<see cref="Product.RegisterClick"/>) na mesma transacao
    /// implicita do <c>SaveChangesAsync</c> (design.md secao 4 — atomicidade garantida pelo
    /// Postgres, sem necessidade de SQL bruto neste volume).
    /// </summary>
    [HttpPost("{id:guid}/click")]
    [EnableRateLimiting(RateLimiterConfigurator.PublicWritePolicy)]
    public async Task<IActionResult> RegisterClick(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.FindAsync(new object[] { id }, ct);
        if (product is null)
            return Accepted();

        product.RegisterClick();
        _db.ProductClicks.Add(new ProductClick(id));
        await _db.SaveChangesAsync(ct);

        return Accepted();
    }
}
