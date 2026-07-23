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
    /// Reprocessa manualmente um item com falha (Issue #11 / Sub-B, CA-B9/CA-B10, #82): volta a
    /// Status=Scheduled com ScheduledAt=now, tornando o item elegivel novamente para o
    /// PublisherJob (Issue #6/#7). RetryCount e ErrorMessage sao zerados — o retry manual e uma
    /// nova tentativa "do zero", independente do limite automatico de 3 tentativas do job.
    /// So permitido quando o item esta em Failed; chamador (controller) deve validar antes.
    /// </summary>
    public void Retry()
    {
        if (Status != PublicationStatus.Failed)
            throw new InvalidOperationException(
                "Somente itens com status Failed podem ser reprocessados manualmente.");

        Status = PublicationStatus.Scheduled;
        ScheduledAt = DateTime.UtcNow;
        RetryCount = 0;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marca manualmente um item como publicado (Issue #13 / Sub-D, #106): usado pela tela
    /// "Facebook Manual", onde o operador publica manualmente no Facebook e depois confirma
    /// no dashboard. So permitido quando o item esta em ManualPending; chamador (controller)
    /// deve validar antes.
    /// </summary>
    public void MarkAsPublishedManually()
    {
        if (Status != PublicationStatus.ManualPending)
            throw new InvalidOperationException(
                "Somente itens ManualPending podem ser marcados como publicados manualmente.");

        Status = PublicationStatus.Published;
        PublishedAt = DateTime.UtcNow;
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
