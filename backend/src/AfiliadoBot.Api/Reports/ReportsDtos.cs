namespace AfiliadoBot.Api.Reports;

/// <summary>
/// Resposta de GET /api/reports/products/summary (Issue #228/T-02). Sem paginacao — total +
/// 4 quebras agregadas (por Plataforma/Categoria/Status/Subcategoria) sobre a mesma base
/// filtrada. Sem resultado (nenhum produto casa o filtro): Total = 0 e as 4 listas vazias,
/// sempre 200 OK (nunca erro) — CA 1.3/2.7.
/// </summary>
public record ProductsReportSummaryDto(
    int Total,
    IReadOnlyList<PlatformCountDto> ByPlatform,
    IReadOnlyList<CategoryCountDto> ByCategory,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<SubcategoryCountDto> BySubcategory);

/// <summary>Quebra de contagem por Plataforma (Issue #228/T-02).</summary>
public record PlatformCountDto(string Platform, int Count);

/// <summary>Quebra de contagem por Categoria (Issue #228/T-02).</summary>
public record CategoryCountDto(string Category, int Count);

/// <summary>Quebra de contagem por Status (Issue #228/T-02).</summary>
public record StatusCountDto(string Status, int Count);

/// <summary>
/// Quebra de contagem por Subcategoria (Issue #228/T-02). Subcategory pode ser null (produtos
/// sem subcategoria detectada) — o grupo aparece com Subcategory = null nesse caso.
/// </summary>
public record SubcategoryCountDto(string? Subcategory, int Count);
