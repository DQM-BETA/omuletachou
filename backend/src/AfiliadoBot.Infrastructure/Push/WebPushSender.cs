using WebPush;

namespace AfiliadoBot.Infrastructure.Push;

/// <summary>
/// Implementacao real de <see cref="IWebPushSender"/>, delegando ao <see cref="WebPushClient"/>
/// da lib WebPush (web-push-libs). Sem estado entre chamadas (VapidDetails e passado a cada
/// envio, ver PushNotificationService).
/// </summary>
public class WebPushSender : IWebPushSender
{
    private readonly WebPushClient _client = new();

    public Task SendAsync(
        PushSubscription subscription,
        string payloadJson,
        VapidDetails vapidDetails,
        CancellationToken ct = default)
        => _client.SendNotificationAsync(subscription, payloadJson, vapidDetails, cancellationToken: ct);
}
