using System.Globalization;
using System.Net;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebPush;
using DomainPushSubscription = AfiliadoBot.Domain.Entities.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace AfiliadoBot.Infrastructure.Push;

public interface IPushNotificationService
{
    /// <summary>
    /// Notificacao individual: exatamente 1 produto publicado com sucesso no Telegram no
    /// ciclo do PublisherJob (especificacao-tecnica.md §3, design.md).
    /// </summary>
    Task SendIndividualAsync(Product product, CancellationToken ct = default);

    /// <summary>
    /// Notificacao consolidada: mais de 1 produto publicado com sucesso no Telegram no mesmo
    /// ciclo do PublisherJob — nunca uma notificacao por produto.
    /// </summary>
    Task SendConsolidatedAsync(int count, CancellationToken ct = default);
}

/// <summary>
/// Envio efetivo de Web Push (NuGet WebPush) para todas as <see cref="PushSubscription"/>
/// cadastradas (Issue #14 / Sub-A). VAPID keys lidas de app_settings a cada chamada (nao
/// cachear em memoria — permite rotacionar sem reiniciar o processo, especificacao-tecnica.md
/// §3). Subscription com HTTP 410 Gone e removida do banco automaticamente; qualquer outra
/// falha e logada como warning, sem interromper o envio as demais subscriptions do lote.
/// </summary>
public class PushNotificationService : IPushNotificationService
{
    private const string VapidSubject = "mailto:contato@omuletachou.com.br";
    private const string VapidPublicKeySetting = "push.vapid_public_key";
    private const string VapidPrivateKeySetting = "push.vapid_private_key";

    private readonly AfiliadoBotDbContext _db;
    private readonly IWebPushSender _sender;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(AfiliadoBotDbContext db, IWebPushSender sender, ILogger<PushNotificationService> logger)
    {
        _db = db;
        _sender = sender;
        _logger = logger;
    }

    public Task SendIndividualAsync(Product product, CancellationToken ct = default)
    {
        var image = !string.IsNullOrWhiteSpace(product.MediaUrl) ? product.MediaUrl : product.MediaLocalPath;
        var discountPct = (int)Math.Round(product.DiscountPct, MidpointRounding.AwayFromZero);

        var payload = JsonSerializer.Serialize(new
        {
            title = "Nova oferta do Mulet 🔥",
            body = $"{product.Title} — R$ {product.SalePrice.ToString("0.00", CultureInfo.InvariantCulture)} ({discountPct}% OFF)",
            icon = "/icon-192x192.png",
            image,
            data = new { url = $"https://omuletachou.com.br/oferta/{product.Slug}" },
        });

        return SendToAllAsync(payload, ct);
    }

    public Task SendConsolidatedAsync(int count, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = "Nova oferta do Mulet 🔥",
            body = $"{count} novas ofertas hoje! Confira no site 👀",
            icon = "/icon-192x192.png",
            data = new { url = "https://omuletachou.com.br" },
        });

        return SendToAllAsync(payload, ct);
    }

    private async Task SendToAllAsync(string payloadJson, CancellationToken ct)
    {
        var publicKey = await GetSettingAsync(VapidPublicKeySetting, ct);
        var privateKey = await GetSettingAsync(VapidPrivateKeySetting, ct);

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            _logger.LogWarning(
                "PushNotificationService: VAPID keys ainda nao cadastradas em app_settings, envio ignorado.");
            return;
        }

        var vapidDetails = new VapidDetails(VapidSubject, publicKey, privateKey);
        var subscriptions = await _db.PushSubscriptions.ToListAsync(ct);

        if (subscriptions.Count == 0)
            return;

        var toRemove = new List<DomainPushSubscription>();

        foreach (var subscription in subscriptions)
        {
            try
            {
                var webPushSubscription = new WebPushSubscription(
                    subscription.Endpoint, subscription.P256dh, subscription.Auth);

                await _sender.SendAsync(webPushSubscription, payloadJson, vapidDetails, ct);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone)
            {
                _logger.LogInformation(
                    "PushNotificationService: subscription {Endpoint} retornou 410 Gone, removendo do banco.",
                    subscription.Endpoint);
                toRemove.Add(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "PushNotificationService: falha ao enviar push para a subscription {Endpoint}.",
                    subscription.Endpoint);
            }
        }

        if (toRemove.Count > 0)
            _db.PushSubscriptions.RemoveRange(toRemove);

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct) =>
        await _db.AppSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
}
