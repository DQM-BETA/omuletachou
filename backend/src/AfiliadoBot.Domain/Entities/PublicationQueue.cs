using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Entities;

public class PublicationQueue
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public SocialNetwork SocialNetwork { get; private set; }
    public PublicationStatus Status { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Product? Product { get; private set; }
    public ICollection<PublicationLog> Logs { get; private set; } = new List<PublicationLog>();

    public bool CanRetry => RetryCount < 3 && Status == PublicationStatus.Failed;

    // Construtor para EF Core
    private PublicationQueue() { }

    public PublicationQueue(Guid productId, SocialNetwork socialNetwork, DateTime scheduledAt)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        SocialNetwork = socialNetwork;
        ScheduledAt = scheduledAt;
        Status = PublicationStatus.Scheduled;
        RetryCount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Forca o status ManualPending (usado para Facebook, ProcessorJob Issue #6), que fica
    /// pendente de acao manual em vez de agendamento automatico. ScheduledAt e mantido apenas
    /// como valor informativo (nao usado pelo Publisher nesse status).
    /// </summary>
    public void MarkAsManualPending()
    {
        Status = PublicationStatus.ManualPending;
    }

    /// <summary>
    /// Registra uma tentativa de publicacao.
    /// Sucesso: Status=Published, PublishedAt=UtcNow.
    /// Falha: RetryCount++, ErrorMessage=errorMessage, Status=Failed.
    /// </summary>
    public void RegisterAttempt(bool success, string? errorMessage = null)
    {
        if (success)
        {
            Status = PublicationStatus.Published;
            PublishedAt = DateTime.UtcNow;
            ErrorMessage = null;
        }
        else
        {
            RetryCount++;
            ErrorMessage = errorMessage;
            Status = PublicationStatus.Failed;
        }
    }
}
