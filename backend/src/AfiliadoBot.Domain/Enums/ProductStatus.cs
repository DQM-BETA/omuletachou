namespace AfiliadoBot.Domain.Enums;

public enum ProductStatus
{
    Pending,
    Queued,
    Published,
    Rejected,
    Processing,
    Error,
    AwaitingAffiliateLink // NOVO — Issue #182/#184: fluxo semi-manual de link de afiliado ML
}
