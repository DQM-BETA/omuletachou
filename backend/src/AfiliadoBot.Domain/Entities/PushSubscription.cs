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
}
