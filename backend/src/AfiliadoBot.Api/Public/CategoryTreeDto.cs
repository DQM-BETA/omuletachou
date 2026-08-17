namespace AfiliadoBot.Api.Public;

/// <summary>
/// Arvore Category -> [Subcategory] retornada por <c>GET /api/public/categories</c>
/// (Issue #167, CA 6.7). Contem apenas categorias/subcategorias com ao menos 1 produto
/// <c>Published</c> ativo, com a contagem correspondente.
/// </summary>
public class CategoryTreeDto
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
    public List<SubcategoryCountDto> Subcategories { get; init; } = [];
}

/// <summary>
/// Contagem de produtos <c>Published</c> ativos de uma subcategoria dentro de uma
/// <see cref="CategoryTreeDto"/> (Issue #167, CA 6.7).
/// </summary>
public class SubcategoryCountDto
{
    public string Subcategory { get; init; } = string.Empty;
    public int Count { get; init; }
}
