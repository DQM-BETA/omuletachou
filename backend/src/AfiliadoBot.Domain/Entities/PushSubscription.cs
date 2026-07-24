namespace AfiliadoBot.Domain.Entities;

public class PushSubscription
{
    public Guid Id { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // Construtor para EF Core
    private PushSubscription() { }

    public PushSubscription(string endpoint, string p256dh, string auth)
    {
        Id = Guid.NewGuid();
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Renova as chaves de criptografia (P256dh/Auth) e o CreatedAt de uma subscription
    /// existente. Usado no resubscribe do mesmo endpoint (upsert silencioso, Issue #14,
    /// criterios-aceite.md secao "Subscription — subscribe/unsubscribe"): quando o browser
    /// gera um novo par de chaves para o mesmo endpoint (ex.: usuario limpou o cache),
    /// o registro existente precisa refletir os novos valores em vez de manter os antigos.
    /// </summary>
    public void Renew(string p256dh, string auth)
    {
        P256dh = p256dh;
        Auth = auth;
        CreatedAt = DateTime.UtcNow;
    }
}
