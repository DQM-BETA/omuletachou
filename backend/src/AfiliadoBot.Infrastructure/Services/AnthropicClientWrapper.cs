using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace AfiliadoBot.Infrastructure.Services;

public class AnthropicClientWrapper : IAnthropicClientWrapper
{
    private readonly string _apiKey;
    private readonly string _model;

    public AnthropicClientWrapper(string apiKey, string model)
    {
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? AnthropicModels.Claude45Haiku : model;
    }

    public async Task<ClaudeCompletionResult> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var client = new AnthropicClient(_apiKey);

        var messages = new List<Message>
        {
            new Message(RoleType.User, userMessage)
        };

        var parameters = new MessageParameters
        {
            Model = _model,
            MaxTokens = 1024,
            System = new List<SystemMessage> { new SystemMessage(systemPrompt) },
            Messages = messages
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ct);
        var text = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;

        // Issue #167 (Sub-B/#169): a resposta ja traz o uso real de tokens (Usage.InputTokens/
        // OutputTokens) — nao estimamos mais nada na mao (design.md §3.1). InputTokens/
        // OutputTokens ficam 0 quando Usage vier nulo (nao deveria acontecer na API real, mas
        // evita NullReferenceException em cenarios inesperados).
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;

        return new ClaudeCompletionResult(text, inputTokens, outputTokens);
    }
}
