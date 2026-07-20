using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Api.Common;

/// <summary>
/// Normalizacao e materializacao de paginacao (Issue #11, especificacao-tecnica.md §4).
/// Regras: page &lt; 1 -&gt; 1; pageSize &lt; 1 -&gt; 20 (default); pageSize &gt; 100 -&gt; 100 (truncamento,
/// CA-D7); sem parametros -&gt; page=1, pageSize=20 (CA-D6).
/// </summary>
public static class PaginationExtensions
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) NormalizePagination(int? page, int? pageSize)
    {
        var normalizedPage = page is null || page < 1 ? 1 : page.Value;
        var normalizedPageSize = pageSize switch
        {
            null => DefaultPageSize,
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize.Value,
        };
        return (normalizedPage, normalizedPageSize);
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize);

        var totalItems = await query.CountAsync(ct);
        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems,
        };
    }
}
