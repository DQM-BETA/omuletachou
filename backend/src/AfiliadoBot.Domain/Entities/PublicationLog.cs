using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Entities;

public class PublicationLog
{
    public Guid Id { get; private set; }
    public Guid PublicationQueueId { get; private set; }
    public SocialNetwork SocialNetwork { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    public PublicationQueue? PublicationQueue { get; private set; }

    // Construtor para EF Core
    private PublicationLog() { }

    public PublicationLog(Guid publicationQueueId, SocialNetwork socialNetwork, bool success, string? errorMessage = null)
    {
        Id = Guid.NewGuid();
        PublicationQueueId = publicationQueueId;
        SocialNetwork = socialNetwork;
        AttemptedAt = DateTime.UtcNow;
        Success = success;
        ErrorMessage = errorMessage;
    }
}
