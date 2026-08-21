using AfiliadoBot.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace AfiliadoBot.Api.Public;

/// <summary>
/// DTO exposto por <c>PublicController</c> (Issue #11 / Sub-D). Contem APENAS os campos
/// autorizados pelo Gerente no Gate 1 (CA-D2/CA-D3): Title, SalePrice, OriginalPrice,
/// DiscountPct, AffiliateLink, MediaUrl, MediaLocalPath (como URL publica), Slug, Category,
/// Subcategory, CollectedAt, Platform. NUNCA inclui ExternalId, AiScore, AiReason ou qualquer
/// campo de app_settings — e um DTO explicito (nunca serializa a entidade Product diretamente).
/// Issue #167 (CA 5.1/5.2/5.3) removeu <c>Platform</c> deste contrato publico (higiene — a
/// distincao de plataforma de origem deixou de ser exposta como filtro/navegacao). A Issue #229
/// (Gate 1) reintroduziu o campo de forma parcial e intencional: agora ele volta como dado de
/// exibicao (string bruta do enum, sem traducao — a tradução para texto amigavel e
/// responsabilidade do frontend), nao mais como filtro/navegacao. O DTO interno/dashboard
/// (<c>ProductDtos.cs</c>, usado por <c>ProductsController</c>) nunca foi tocado e sempre expos
/// <c>Platform</c> normalmente.
/// Issue #231 (sub-issue #278, especificacao-tecnica.md §4.1): <c>Id</c> (uuid) adicionado — o
/// frontend precisa dele para identificar o produto no registro de clique (T-04,
/// <c>POST /api/public/products/{id}/click</c>). Nao e dado sensivel (uuid interno opaco, ja
/// usado como PK publica em outras APIs REST comuns) — so nao estava no DTO antes por nao haver
/// necessidade (a rota de clique nao existia).
/// </summary>
public class PublicDealDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal SalePrice { get; init; }
    public decimal OriginalPrice { get; init; }
    public decimal DiscountPct { get; init; }
    public string? AffiliateLink { get; init; }
    public string? MediaUrl { get; init; }
    public string? MediaLocalPath { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Subcategory { get; init; }
    public DateTime CollectedAt { get; init; }
    public string? Platform { get; init; }

    /// <summary>
    /// Constroi o DTO a partir da entidade, convertendo MediaLocalPath (caminho fisico em disco,
    /// ex. "/app/media/xxx.mp4") em URL publica servida por app.UseStaticFiles em "/media"
    /// (Program.cs). CollectedAt reaproveita Product.CreatedAt (momento da coleta original).
    /// </summary>
    public static PublicDealDto FromProduct(Product product, HttpRequest request)
    {
        string? mediaLocalPathUrl = null;
        if (!string.IsNullOrWhiteSpace(product.MediaLocalPath))
        {
            var fileName = Path.GetFileName(product.MediaLocalPath);
            mediaLocalPathUrl = $"{request.Scheme}://{request.Host}/media/{fileName}";
        }

        return new PublicDealDto
        {
            Id = product.Id,
            Title = product.Title,
            SalePrice = product.SalePrice,
            OriginalPrice = product.OriginalPrice,
            DiscountPct = product.DiscountPct,
            AffiliateLink = product.AffiliateLink,
            MediaUrl = product.MediaUrl,
            MediaLocalPath = mediaLocalPathUrl,
            Slug = product.Slug,
            Category = product.Category,
            Subcategory = product.Subcategory,
            CollectedAt = product.CreatedAt,
            Platform = product.Platform.ToString(),
        };
    }
}
