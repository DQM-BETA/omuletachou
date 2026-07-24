using WebPush;

namespace AfiliadoBot.Infrastructure.Push;

/// <summary>
/// Abstracao fina sobre <see cref="WebPushClient"/> (NuGet WebPush), exclusivamente para
/// permitir teste unitario de <see cref="PushNotificationService"/> (tratamento de 410 Gone,
/// agrupamento individual/consolidado) sem depender de envio HTTP real — WebPushClient nao
/// expoe interface propria (especificacao-tecnica.md §3/§8).
/// </summary>
public interface IWebPushSender
{
    Task SendAsync(
        PushSubscription subscription,
        string payloadJson,
        VapidDetails vapidDetails,
        CancellationToken ct = default);
}
