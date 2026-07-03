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

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
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
        return response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
    }
}
