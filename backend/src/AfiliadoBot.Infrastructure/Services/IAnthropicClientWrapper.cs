namespace AfiliadoBot.Infrastructure.Services;

public interface IAnthropicClientWrapper
{
    Task<ClaudeCompletionResult> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
