namespace AfiliadoBot.Infrastructure.Services;

/// <summary>
/// Orcamento mensal do fallback de categorizacao via Claude (Issue #167 — CA 4.1-4.5). Escopado
/// SOMENTE a chamada de categorizacao (ClaudeAiService.ClassifyCategoryAsync) — scoring
/// (ScoreProductAsync) e legenda (GenerateCaptionAsync) nunca dependem deste servico
/// (design.md §3.2, CA 3.4/4.4).
/// </summary>
public interface IClaudeBudgetService
{
    /// <summary>
    /// True quando o gasto acumulado do mes corrente (reset lazy — CA 4.5) ainda esta abaixo de
    /// "claude.monthly_budget_limit_brl". Leitura simples, sem lock (design.md §3.5) — a unica
    /// escrita (RecordUsageAsync) e atomica via UPDATE...CASE no Postgres.
    /// </summary>
    Task<bool> IsCategorizationBudgetAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Debita o custo estimado (tokens reais x preco/cambio configurados em app_settings) do
    /// acumulado do mes corrente. So deve ser chamado apos uma chamada Claude bem-sucedida
    /// (CA 4.2 — "executada com sucesso").
    /// </summary>
    Task RecordUsageAsync(int inputTokens, int outputTokens, CancellationToken ct = default);
}
