namespace AfiliadoBot.Api.Queue;

/// <summary>Item de listagem de GET /api/queue (CA-B7).</summary>
public record QueueItemDto(
    Guid Id,
    Guid ProductId,
    string SocialNetwork,
    string Status,
    DateTime ScheduledAt,
    DateTime? PublishedAt,
    int RetryCount,
    string? ErrorMessage,
    DateTime CreatedAt);
