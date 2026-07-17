using System.Text.Json.Serialization;

namespace AfiliadoBot.Api.Products;

/// <summary>
/// Item de listagem de GET /api/products (CA-B1/CA-B2). Sem ai_score/ai_reason — esses
/// campos so aparecem no detalhe (CA-B3).
/// </summary>
public record ProductListItemDto(
    Guid Id,
    string Title,
    decimal SalePrice,
    decimal OriginalPrice,
    decimal DiscountPct,
    string Status,
    string Platform,
    string Slug,
    string Category,
    DateTime CreatedAt);

/// <summary>
/// Detalhe de GET /api/products/{id} (CA-B3). ai_score/ai_reason em snake_case, formato exigido
/// explicitamente pelo CA — os demais campos seguem o camelCase padrao do ASP.NET Core.
/// </summary>
public record ProductDetailDto(
    Guid Id,
    string Title,
    string Description,
    decimal SalePrice,
    decimal OriginalPrice,
    decimal DiscountPct,
    string? AffiliateLink,
    string? ImageUrl,
    string? MediaUrl,
    string? MediaLocalPath,
    string Slug,
    string Category,
    string Platform,
    string Status,
    [property: JsonPropertyName("ai_score")] int? AiScore,
    [property: JsonPropertyName("ai_reason")] string? AiReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
