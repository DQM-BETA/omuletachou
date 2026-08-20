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

    /// <summary>
    /// Campo aditivo (Issue #260, sub-issue #268 — design.md §2.5, especificacao-tecnica.md §3).
    /// <c>null</c> quando o consumidor nao usa busca textual por <c>q</c> (default — todos os
    /// consumidores existentes de <c>PagedResult&lt;T&gt;</c>, ex. ProductsController/
    /// QueueController, permanecem inalterados). Quando <c>PublicController.GetDeals</c> recebe
    /// <c>q</c> relevante: <c>false</c> = resultado veio do estagio 1 (full-text, match "de
    /// verdade"); <c>true</c> = resultado veio do estagio 2 (fallback trigram, aproximado).
    /// </summary>
    public bool? IsApproximateSearch { get; init; }
}
