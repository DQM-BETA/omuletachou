namespace AfiliadoBot.Api.Queue;

/// <summary>
/// Payload de PATCH /api/queue/{id}/status (Issue #13 / Sub-D, #106). Unica transicao
/// explicita suportada hoje: ManualPending -> Published (Facebook Manual).
/// </summary>
public record UpdateQueueStatusRequest(string Status);
