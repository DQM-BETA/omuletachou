namespace AfiliadoBot.Infrastructure.Services;

public interface IAnthropicClientWrapper
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
