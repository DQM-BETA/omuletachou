using AfiliadoBot.Api.Common;
using AfiliadoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace AfiliadoBot.Api.Public;

/// <summary>
/// Busca textual em 2 estagios de <c>PublicController.GetDeals</c> (Issue #260, sub-issue #268 —
/// design.md §2.1/§3, especificacao-tecnica.md §2/§3). Extraida do controller (recomendacao
/// "(b)" de especificacao-tecnica.md §4) para manter <c>GetDeals</c> legivel e para poder ser
/// testada direto contra o <c>DbContext</c> via Testcontainers, sem <c>WebApplicationFactory</c>
/// (o provider InMemory usado pela maioria dos testes existentes nao suporta
/// <c>tsvector</c>/<c>pg_trgm</c>).
///
/// Estagio 1 (full-text): usa a coluna gerada/indexada <c>search_vector</c> (shadow property
/// "SearchVector", ProductConfiguration.ConfigureSearchVector — Issue #260/#267) via
/// <c>EF.Property&lt;NpgsqlTsVector&gt;</c>, comparada com <c>plainto_tsquery('portuguese', q)</c>
/// (nao <c>to_tsquery</c> — evita exigir sintaxe de operador do usuario e erro de parse com
/// caracteres especiais, especificacao-tecnica.md §2). Ranking via <c>ts_rank</c>.
///
/// Estagio 2 (fallback trigram, so roda quando o estagio 1 devolve zero): <c>similarity()</c>
/// (pg_trgm) sobre Title/Category/Description, threshold <see cref="SearchConstants.ApproximateSimilarityThreshold"/>.
/// O predicado <c>GREATEST(sim(title), sim(category), sim(description)) &gt;= threshold</c>
/// (design.md §2.2) e expresso aqui como OR de 3 comparacoes individuais — equivalente logico
/// (GREATEST(a,b,c) &gt;= t &lt;=&gt; a&gt;=t || b&gt;=t || c&gt;=t) que evita depender de traducao LINQ para
/// <c>Math.Max</c> (sem suporte confirmado no provider Npgsql 8.0.11 — nenhuma traducao para
/// GREATEST/LEAST encontrada em NpgsqlMathTranslator). O ranking (ORDER BY) usa a soma ponderada
/// diretamente (0.60/0.25/0.15 — design.md §2.2), que so precisa de +, * e chamadas de funcao,
/// sem Math.Max.
/// </summary>
public class ProductSearchService
{
    /// <summary>
    /// <c>q</c> e considerado relevante quando nao e nulo/vazio/whitespace e, apos
    /// <c>Trim()</c>, tem pelo menos <see cref="SearchConstants.MinQueryLength"/> caracteres
    /// (CA E.1, especificacao-tecnica.md §2.6).
    /// </summary>
    public static bool IsRelevant(string? q) =>
        !string.IsNullOrWhiteSpace(q) && q.Trim().Length >= SearchConstants.MinQueryLength;

    /// <summary>
    /// Executa a busca em 2 estagios sobre <paramref name="baseQuery"/> (ja filtrada por
    /// Status/category/subcategory/minPrice/maxPrice — design.md §2.6: <c>q</c> e so mais um
    /// <c>.Where()</c> na mesma <c>IQueryable</c>, AND automatico com os demais filtros).
    /// Pressupoe <see cref="IsRelevant"/> == true para <paramref name="q"/> (chamador decide).
    /// </summary>
    public async Task<(PagedResult<Product> Paged, bool? IsApproximateSearch)> SearchAsync(
        IQueryable<Product> baseQuery, string q, int? page, int? pageSize, CancellationToken ct)
    {
        var term = q.Trim();

        var tier1Paged = await SearchTier1Async(baseQuery, term, page, pageSize, ct);
        if (tier1Paged.TotalItems > 0)
            return (tier1Paged, false);

        var tier2Paged = await SearchTier2Async(baseQuery, term, page, pageSize, ct);
        return (tier2Paged, tier2Paged.TotalItems > 0);
    }

    private static async Task<PagedResult<Product>> SearchTier1Async(
        IQueryable<Product> baseQuery, string term, int? page, int? pageSize, CancellationToken ct)
    {
        // EF.Functions.PlainToTsQuery so pode ser usado DENTRO de uma expressao LINQ traduzida
        // pelo provider — chamar fora de uma lambda (ex.: atribuir a uma variavel antes do
        // .Where()) forca client-evaluation e lanca InvalidOperationException em runtime (o
        // metodo e um stub sem corpo real, existe apenas para casar com o
        // NpgsqlFullTextSearchMethodTranslator). Por isso a chamada e repetida dentro de cada
        // lambda (Where/OrderByDescending) em vez de fatorada em uma variavel comum.
        var tier1 = baseQuery
            .Where(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                .Matches(EF.Functions.PlainToTsQuery("portuguese", term)))
            .OrderByDescending(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                .Rank(EF.Functions.PlainToTsQuery("portuguese", term)));

        return await tier1.ToPagedResultAsync(page, pageSize, ct);
    }

    private static async Task<PagedResult<Product>> SearchTier2Async(
        IQueryable<Product> baseQuery, string term, int? page, int? pageSize, CancellationToken ct)
    {
        const double threshold = SearchConstants.ApproximateSimilarityThreshold;

        var tier2 = baseQuery
            .Where(p =>
                EF.Functions.TrigramsSimilarity(p.Title, term) >= threshold ||
                EF.Functions.TrigramsSimilarity(p.Category, term) >= threshold ||
                EF.Functions.TrigramsSimilarity(p.Description, term) >= threshold)
            .OrderByDescending(p =>
                0.60 * EF.Functions.TrigramsSimilarity(p.Title, term) +
                0.25 * EF.Functions.TrigramsSimilarity(p.Category, term) +
                0.15 * EF.Functions.TrigramsSimilarity(p.Description, term));

        return await tier2.ToPagedResultAsync(page, pageSize, ct);
    }
}
