using AfiliadoBot.Api.Common;
using AfiliadoBot.Api.Products;
using AfiliadoBot.Domain.Entities;
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

        var pagedProducts = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToPagedResultAsync(page, pageSize, ct);

        // Destinos (site + redes sociais) sao agregados numa unica query adicional sobre a pagina
        // atual de produtos (evita N+1 — Issue #208/T-02, especificacao-tecnica.md §1.1).
        var productIds = pagedProducts.Items.Select(p => p.Id).ToList();
        var destinationsByProduct = await BuildDestinationsByProductAsync(productIds, ct);

        var items = pagedProducts.Items
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
                p.AiScore,
                p.AiReason,
                p.CreatedAt,
                p.SourceUrl,
                BuildDestinations(p, destinationsByProduct.GetValueOrDefault(p.Id))))
            .ToList();

        var result = new PagedResult<ProductListItemDto>
        {
            Items = items,
            Page = pagedProducts.Page,
            PageSize = pagedProducts.PageSize,
            TotalItems = pagedProducts.TotalItems,
        };

        return Ok(result);
    }

    /// <summary>
    /// Busca, numa unica query, a linha mais recente (maior CreatedAt) de PublicationQueue por
    /// (ProductId, SocialNetwork) para os produtos informados (Issue #208/T-02). Agrupamento feito
    /// em memoria apos materializar a query — mesmo criterio ja usado em GetProduct/facebookCaption.
    /// </summary>
    private async Task<Dictionary<Guid, List<PublicationQueue>>> BuildDestinationsByProductAsync(
        List<Guid> productIds, CancellationToken ct)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, List<PublicationQueue>>();

        var queueEntries = await _db.PublicationQueues
            .AsNoTracking()
            .Where(q => productIds.Contains(q.ProductId))
            .ToListAsync(ct);

        return queueEntries
            .GroupBy(q => q.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(q => q.SocialNetwork)
                    .Select(sg => sg.OrderByDescending(q => q.CreatedAt).First())
                    .ToList());
    }

    /// <summary>
    /// Monta a lista de destinos de um produto: "Site" so quando Published, e uma entrada para
    /// cada valor de SocialNetwork com o status agregado (Issue #208/T-02, especificacao-tecnica.md
    /// §1.1).
    /// </summary>
    private static List<PublicationDestinationDto> BuildDestinations(
        Product product, List<PublicationQueue>? latestByNetwork)
    {
        var destinations = new List<PublicationDestinationDto>();

        if (product.Status == ProductStatus.Published)
            destinations.Add(new PublicationDestinationDto("Site", "Published"));

        var latestBySocialNetwork = (latestByNetwork ?? [])
            .ToDictionary(q => q.SocialNetwork, q => q.Status);

        foreach (var network in Enum.GetValues<SocialNetwork>())
        {
            var status = latestBySocialNetwork.TryGetValue(network, out var queueStatus)
                ? MapPublicationStatus(queueStatus)
                : "NotApplicable";

            destinations.Add(new PublicationDestinationDto(network.ToString(), status));
        }

        return destinations;
    }

    private static string MapPublicationStatus(PublicationStatus status) => status switch
    {
        PublicationStatus.Scheduled => "Pending",
        PublicationStatus.ManualPending => "Pending",
        PublicationStatus.Published => "Published",
        PublicationStatus.Failed => "Failed",
        _ => "NotApplicable",
    };

    /// <summary>CA-B3 (inclui ai_score/ai_reason), CA-B4 (404 quando inexistente).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailDto>> GetProduct(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return NotFound();

        var facebookCaption = await _db.PublicationQueues
            .AsNoTracking()
            .Where(q => q.ProductId == id && q.SocialNetwork == SocialNetwork.Facebook)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => (string?)q.Caption)
            .FirstOrDefaultAsync(ct);

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
            facebookCaption,
            product.CreatedAt,
            product.UpdatedAt);

        return Ok(dto);
    }

    /// <summary>
    /// CA-B5 (valor valido, pending/rejected), CA-B6 (400 sem alterar quando fora do enum
    /// permitido). CA-B4-like: 404 quando o produto nao existe (checado apos a validacao do
    /// valor recebido, para nao vazar existencia de recurso em payload invalido).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateProductStatusRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ProductStatus>(request.Status, ignoreCase: true, out var newStatus) ||
            (newStatus != ProductStatus.Pending && newStatus != ProductStatus.Rejected))
        {
            return BadRequest(new { message = "Status invalido. Valores permitidos: pending, rejected." });
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return NotFound();

        product.UpdateStatusManually(newStatus);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Issue #182/#184: importa em lote os links de afiliado gerados manualmente pelo operador na
    /// ferramenta oficial do Mercado Livre. Pareamento produto/link e feito EXPLICITAMENTE por
    /// ProductId no corpo da requisicao (montado pelo dashboard, que ja tem o ProductId de cada
    /// linha exibida) — nao por ordem/posicao inferida no servidor, para nao quebrar se a lista de
    /// AwaitingAffiliateLink mudar entre a exportacao e a importacao (produto novo entrando em espera
    /// no meio do processo, por exemplo). Nunca falha o lote inteiro por um item invalido (mesmo
    /// principio de isolamento de falha ja usado no resto do projeto) — cada item e validado e
    /// reportado individualmente no resultado.
    /// </summary>
    [HttpPost("affiliate-links/import")]
    public async Task<ActionResult<ImportAffiliateLinksResult>> ImportAffiliateLinks(
        [FromBody] ImportAffiliateLinksRequest request,
        CancellationToken ct)
    {
        var skipped = new List<AffiliateLinkImportSkip>();
        var imported = 0;

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.AffiliateLink))
            {
                skipped.Add(new AffiliateLinkImportSkip(item.ProductId, "Link vazio"));
                continue;
            }

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
            if (product is null)
            {
                skipped.Add(new AffiliateLinkImportSkip(item.ProductId, "Produto nao encontrado"));
                continue;
            }

            if (product.Status != ProductStatus.AwaitingAffiliateLink)
            {
                skipped.Add(new AffiliateLinkImportSkip(
                    item.ProductId,
                    $"Status atual e {product.Status}, esperado AwaitingAffiliateLink"));
                continue;
            }

            product.ResolveAffiliateLink(item.AffiliateLink.Trim());
            imported++;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new ImportAffiliateLinksResult(imported, skipped));
    }
}
