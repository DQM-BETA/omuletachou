using System.Text.Json.Serialization;

namespace AfiliadoBot.Api.Products;

/// <summary>
/// Item de listagem de GET /api/products (CA-B1/CA-B2). Inclui ai_score/ai_reason
/// (extensao aditiva, Issue #13/Sub-B — dashboard exibe a pontuacao de IA na propria tabela,
/// mesmo [JsonPropertyName] snake_case do detalhe, ver ProductDetailDto).
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
    [property: JsonPropertyName("ai_score")] int? AiScore,
    [property: JsonPropertyName("ai_reason")] string? AiReason,
    DateTime CreatedAt,
    string? SourceUrl); // NOVO — Issue #184, campo aditivo ao final (nao quebra consumidores existentes)

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
    [property: JsonPropertyName("ai_caption")] string? AiCaption,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Body de PATCH /api/products/{id}/status (CA-B5/CA-B6). Valores aceitos: "pending" ou
/// "rejected" (case-insensitive) — demais valores do enum ProductStatus sao transicoes de
/// sistema, nao disponiveis via este endpoint manual.
/// </summary>
public record UpdateProductStatusRequest(string Status);

/// <summary>
/// Um item do lote de POST /api/products/affiliate-links/import (Issue #182/#184) — pareamento
/// EXPLICITO por ProductId (montado pelo dashboard, que ja tem o ProductId de cada linha
/// exibida), nao por ordem/posicao inferida no servidor.
/// </summary>
public record AffiliateLinkImportItem(Guid ProductId, string AffiliateLink);

/// <summary>Body de POST /api/products/affiliate-links/import (Issue #182/#184).</summary>
public record ImportAffiliateLinksRequest(List<AffiliateLinkImportItem> Items);

/// <summary>Item pulado na importacao em lote, com o motivo (Issue #182/#184).</summary>
public record AffiliateLinkImportSkip(Guid ProductId, string Reason);

/// <summary>Resultado de POST /api/products/affiliate-links/import (Issue #182/#184).</summary>
public record ImportAffiliateLinksResult(int Imported, List<AffiliateLinkImportSkip> Skipped);
