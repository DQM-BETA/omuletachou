using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Entities;

public class Product
{
    // Score threshold para aprovacao por IA
    public const int AiScoreThreshold = 6;

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal SalePrice { get; private set; }
    public decimal OriginalPrice { get; private set; }
    public decimal DiscountPct { get; private set; }
    public string? AffiliateLink { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? MediaUrl { get; private set; }
    public string? MediaType { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public Platform Platform { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public int? AiScore { get; private set; }
    public string? AiReason { get; private set; }
    public string? AiCaption { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<PublicationQueue> PublicationQueues { get; private set; } = new List<PublicationQueue>();

    // Construtor para EF Core
    private Product() { }

    public Product(
        string title,
        string description,
        decimal salePrice,
        decimal originalPrice,
        decimal discountPct,
        string? affiliateLink,
        string slug,
        string category,
        Platform platform,
        string? imageUrl = null,
        string externalId = "",
        string? mediaUrl = null,
        string? mediaType = null)
    {
        if (salePrice < 0)
            throw new ArgumentException("SalePrice nao pode ser negativo.", nameof(salePrice));

        if (discountPct < 0 || discountPct > 100)
            throw new ArgumentException("DiscountPct deve estar entre 0 e 100.", nameof(discountPct));

        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        SalePrice = salePrice;
        OriginalPrice = originalPrice;
        DiscountPct = discountPct;
        AffiliateLink = string.IsNullOrWhiteSpace(affiliateLink) ? null : affiliateLink;
        Slug = slug;
        Category = category;
        Platform = platform;
        ImageUrl = imageUrl;
        ExternalId = externalId;
        MediaUrl = mediaUrl;
        MediaType = mediaType;
        Status = ProductStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atualiza o resultado da avaliacao de IA.
    /// Score >= AiScoreThreshold -> Queued; abaixo -> Rejected.
    /// </summary>
    public void UpdateAiResult(int score, string reason, string caption)
    {
        AiScore = score;
        AiReason = reason;
        AiCaption = caption;
        UpdatedAt = DateTime.UtcNow;

        Status = score >= AiScoreThreshold
            ? ProductStatus.Queued
            : ProductStatus.Rejected;
    }

    public void MarkAsPublished()
    {
        Status = ProductStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atualiza preco/midia de um produto ja existente (upsert por coleta).
    /// Preserva Id, Status, AiScore, Slug e CreatedAt. Nao reexecuta o scoring de IA.
    /// </summary>
    public void UpdateFromCollector(
        decimal salePrice,
        decimal originalPrice,
        decimal discountPct,
        string? imageUrl,
        string? mediaUrl = null,
        string? mediaType = null)
    {
        if (salePrice < 0)
            throw new ArgumentException("SalePrice nao pode ser negativo.", nameof(salePrice));

        if (discountPct < 0 || discountPct > 100)
            throw new ArgumentException("DiscountPct deve estar entre 0 e 100.", nameof(discountPct));

        SalePrice = salePrice;
        OriginalPrice = originalPrice;
        DiscountPct = discountPct;
        ImageUrl = imageUrl;
        MediaUrl = mediaUrl;
        MediaType = mediaType;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Preenche o link de afiliado apos aprovacao do scoring (usado pelo ProcessorJob, Issue #6).
    /// </summary>
    public void SetAffiliateLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            throw new ArgumentException("AffiliateLink nao pode ser nulo ou vazio.", nameof(link));

        AffiliateLink = link;
        UpdatedAt = DateTime.UtcNow;
    }
}
