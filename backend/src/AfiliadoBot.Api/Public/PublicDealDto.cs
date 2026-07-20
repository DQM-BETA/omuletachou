using AfiliadoBot.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace AfiliadoBot.Api.Public;

/// <summary>
/// DTO exposto por <c>PublicController</c> (Issue #11 / Sub-D). Contem APENAS os campos
/// autorizados pelo Gerente no Gate 1 (CA-D2/CA-D3): Title, SalePrice, OriginalPrice,
/// DiscountPct, AffiliateLink, MediaUrl, MediaLocalPath (como URL publica), Slug, Category,
/// CollectedAt, Platform. NUNCA inclui ExternalId, AiScore, AiReason ou qualquer campo de
/// app_settings — e um DTO explicito (nunca serializa a entidade Product diretamente).
/// </summary>
public class PublicDealDto
{
    public string Title { get; init; } = string.Empty;
    public decimal SalePrice { get; init; }
    public decimal OriginalPrice { get; init; }
    public decimal DiscountPct { get; init; }
    public string? AffiliateLink { get; init; }
    public string? MediaUrl { get; init; }
    public string? MediaLocalPath { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public DateTime CollectedAt { get; init; }
    public string Platform { get; init; } = string.Empty;

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
            Title = product.Title,
            SalePrice = product.SalePrice,
            OriginalPrice = product.OriginalPrice,
            DiscountPct = product.DiscountPct,
            AffiliateLink = product.AffiliateLink,
            MediaUrl = product.MediaUrl,
            MediaLocalPath = mediaLocalPathUrl,
            Slug = product.Slug,
            Category = product.Category,
            CollectedAt = product.CreatedAt,
            Platform = product.Platform.ToString(),
        };
    }
}
