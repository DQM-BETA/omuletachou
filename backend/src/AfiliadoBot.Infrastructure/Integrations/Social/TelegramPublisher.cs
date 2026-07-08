using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Domain.Interfaces;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfiliadoBot.Infrastructure.Integrations.Social;

/// <summary>
/// Publisher do Telegram (Issue #7 / #60): envia video (sendVideo), imagem (sendPhoto) ou
/// somente texto (sendMessage, fallback quando nao ha midia) para o canal configurado em
/// app_settings (telegram.bot_token, telegram.channel_id).
/// </summary>
public class TelegramPublisher : ISocialPublisher
{
    private const string BaseUrl = "https://api.telegram.org";

    private readonly HttpClient _httpClient;
    private readonly AfiliadoBotDbContext _dbContext;
    private readonly ILogger<TelegramPublisher> _logger;

    public TelegramPublisher(
        HttpClient httpClient,
        AfiliadoBotDbContext dbContext,
        ILogger<TelegramPublisher> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public SocialNetwork Network => SocialNetwork.Telegram;

    public async Task<bool> PublishAsync(PublicationQueue item, CancellationToken ct = default)
    {
        var (botToken, channelId) = await LoadCredentialsAsync(ct);

        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(channelId))
        {
            throw new InvalidOperationException(
                "Credenciais do Telegram (telegram.bot_token, telegram.channel_id) ausentes ou invalidas.");
        }

        var product = item.Product ?? await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);

        if (product is null)
            throw new InvalidOperationException($"Produto {item.ProductId} nao encontrado para publicacao no Telegram.");

        var caption = product.AiCaption ?? string.Empty;

        var mediaSource = !string.IsNullOrWhiteSpace(product.MediaLocalPath)
            ? product.MediaLocalPath
            : product.MediaUrl;

        HttpResponseMessage response;

        if (string.IsNullOrWhiteSpace(mediaSource))
        {
            _logger.LogWarning(
                "TelegramPublisher: produto {ProductId} sem midia (MediaLocalPath e MediaUrl nulos). Publicando somente com legenda.",
                product.Id);

            response = await SendTextAsync(botToken, channelId, caption, ct);
        }
        else if (string.Equals(product.MediaType, "video", StringComparison.OrdinalIgnoreCase))
        {
            response = await SendMediaAsync(botToken, channelId, "sendVideo", "video", mediaSource, caption, ct);
        }
        else
        {
            response = await SendMediaAsync(botToken, channelId, "sendPhoto", "photo", mediaSource, caption, ct);
        }

        using (response)
        {
            return response.IsSuccessStatusCode;
        }
    }

    private async Task<(string? BotToken, string? ChannelId)> LoadCredentialsAsync(CancellationToken ct)
    {
        var keys = new[] { "telegram.bot_token", "telegram.channel_id" };

        var values = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        values.TryGetValue("telegram.bot_token", out var botToken);
        values.TryGetValue("telegram.channel_id", out var channelId);

        return (botToken, channelId);
    }

    private Task<HttpResponseMessage> SendTextAsync(
        string botToken, string channelId, string caption, CancellationToken ct)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(channelId), "chat_id" },
            { new StringContent(caption), "text" },
            { new StringContent("HTML"), "parse_mode" }
        };

        return _httpClient.PostAsync($"{BaseUrl}/bot{botToken}/sendMessage", content, ct);
    }

    /// <summary>
    /// Envia midia via multipart. Quando <paramref name="mediaSource"/> e um caminho local
    /// existente, envia o conteudo do arquivo; senao (URL remota — fallback MediaUrl), o
    /// Telegram aceita a URL diretamente no campo de midia do multipart.
    /// </summary>
    private async Task<HttpResponseMessage> SendMediaAsync(
        string botToken,
        string channelId,
        string method,
        string mediaField,
        string mediaSource,
        string caption,
        CancellationToken ct)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(channelId), "chat_id" },
            { new StringContent(caption), "caption" },
            { new StringContent("HTML"), "parse_mode" }
        };

        if (File.Exists(mediaSource))
        {
            var bytes = await File.ReadAllBytesAsync(mediaSource, ct);
            var fileContent = new ByteArrayContent(bytes);
            content.Add(fileContent, mediaField, Path.GetFileName(mediaSource));
        }
        else
        {
            // Fallback (MediaUrl): Telegram aceita URL diretamente no campo de midia.
            content.Add(new StringContent(mediaSource), mediaField);
        }

        return await _httpClient.PostAsync($"{BaseUrl}/bot{botToken}/{method}", content, ct);
    }
}
