namespace AfiliadoBot.Api.Public;

/// <summary>
/// Constantes da busca textual em 2 estagios de <c>PublicController.GetDeals</c> (Issue #260,
/// sub-issue #268 — especificacao-tecnica.md §3, design.md §2.3/§2.6).
/// </summary>
public static class SearchConstants
{
    /// <summary>
    /// Tamanho minimo (apos <c>Trim()</c>) para <c>q</c> ser considerado relevante. Abaixo disso
    /// (ausente/vazio/whitespace/1 caractere), a busca e tratada como se <c>q</c> nao existisse
    /// (CA E.1) — sem erro, sem acionar nenhum dos 2 estagios.
    /// </summary>
    public const int MinQueryLength = 2;

    /// <summary>
    /// Threshold de <c>similarity()</c> (pg_trgm, escala 0-1) usado pelo estagio 2 (fallback)
    /// para decidir se um produto e "aproximadamente" relacionado ao termo buscado — abaixo
    /// deliberadamente do default 0.3 do operador <c>%</c> do Postgres (design.md §2.3).
    /// </summary>
    public const double ApproximateSimilarityThreshold = 0.15;
}
