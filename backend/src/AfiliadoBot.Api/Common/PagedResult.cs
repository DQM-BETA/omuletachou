namespace AfiliadoBot.Api.Common;

/// <summary>
/// Envelope de paginacao padrao (Issue #11, especificacao-tecnica.md §4), reaproveitado por
/// ProductsController/QueueController (Sub-B) e demais endpoints de listagem futuros (Sub-D).
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
