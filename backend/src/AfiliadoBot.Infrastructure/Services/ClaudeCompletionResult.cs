namespace AfiliadoBot.Infrastructure.Services;

/// <summary>
/// Resultado bruto de uma chamada ao Claude (Issue #167 — Sub-B/#169): texto da resposta +
/// tokens de entrada/saida reportados pela propria API (Anthropic.SDK, MessageResponse.Usage),
/// sem nenhum calculo de custo aqui — quem consome (ex.: IClaudeBudgetService) decide o que
/// fazer com os tokens (design.md §3.2/§3.6).
/// </summary>
public record ClaudeCompletionResult(string Text, int InputTokens, int OutputTokens);
