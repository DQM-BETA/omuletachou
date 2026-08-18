using AfiliadoBot.Domain.Enums;

namespace AfiliadoBot.Domain.Entities;

public class Product
{
    // Score threshold para aprovacao por IA
    public const int AiScoreThreshold = 6;

    // Coluna "ai_reason" e varchar(300) (ProductConfiguration). Com a IA real (Claude, nao mock,
    // Issue #199) a resposta pode facilmente passar de 300 caracteres — como SaveChangesAsync roda
    // uma unica vez ao final do loop de coleta (MercadoLivreCollector/AmazonCollector/
    // ShopeeCollector), um unico produto com reason estourado derrubava o ciclo inteiro (mesma
    // causa raiz do bug do slug, Issue #199 primeira correcao).
    private const int MaxAiReasonLength = 300;

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
    public string? MediaLocalPath { get; private set; }
    public string? SourceUrl { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string? Subcategory { get; private set; }
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
        string? mediaType = null,
        string? sourceUrl = null,
        string? subcategory = null)
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
        Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory;
        Platform = platform;
        ImageUrl = imageUrl;
        ExternalId = externalId;
        MediaUrl = mediaUrl;
        MediaType = mediaType;
        SourceUrl = sourceUrl;
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
        SetAiReason(reason);
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
    /// Marca o produto como em processamento (lock otimista do ProcessorJob, Issue #6).
    /// Deve ser persistido imediatamente ao pegar o produto, antes de qualquer outra operacao.
    /// </summary>
    public void MarkAsProcessing()
    {
        Status = ProductStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marca o produto como erro nao recuperavel (ProcessorJob, Issue #6).
    /// Reaproveita o campo AiReason para persistir a mensagem descritiva do erro.
    /// </summary>
    public void MarkAsError(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason nao pode ser nulo ou vazio.", nameof(reason));

        Status = ProductStatus.Error;
        SetAiReason(reason);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atribui AiReason truncando para caber em varchar(300) (Issue #199 — segundo bug: a resposta
    /// real da IA pode passar de 300 caracteres). Usado por UpdateAiResult e MarkAsError para nao
    /// duplicar a logica de truncagem.
    /// </summary>
    private void SetAiReason(string? reason)
    {
        AiReason = reason is not null && reason.Length > MaxAiReasonLength
            ? reason[..MaxAiReasonLength]
            : reason;
    }

    /// <summary>
    /// Preenche o caminho local da midia baixada e o tipo detectado (ProcessorJob, Issue #6).
    /// LocalPath nulo indica falha no download (produto segue sem midia local).
    /// </summary>
    public void SetLocalMedia(string? localPath, string? mediaType)
    {
        MediaLocalPath = localPath;
        MediaType = mediaType;
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
        string? mediaType = null,
        string? sourceUrl = null)
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
        if (sourceUrl is not null)
            SourceUrl = sourceUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atualiza categoria/subcategoria detectadas pelo CategoryDetector (Domain.Services,
    /// Issue #167 — movido de Application). So substitui quando a categoria atual ainda for o
    /// fallback "Geral" — nao sobrescreve categoria ja detectada/definida anteriormente com um
    /// valor mais especifico. A guarda cobre tambem Subcategory (Issue #167): so e atualizada
    /// junto de Category, na mesma condicao defensiva.
    /// </summary>
    public void SetCategory(string category, string? subcategory = null)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;

        if (!string.Equals(Category, "Geral", StringComparison.OrdinalIgnoreCase))
            return;

        Category = category;
        Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gera e persiste o slug quando ainda nao preenchido (ProcessorJob, Issue #6).
    /// Nunca regera um slug ja existente.
    /// </summary>
    public void SetSlugIfEmpty(string slug)
    {
        if (!string.IsNullOrWhiteSpace(Slug))
            return;

        if (string.IsNullOrWhiteSpace(slug))
            return;

        Slug = slug;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atualiza o status manualmente via acao do operador no dashboard (Issue #11 / Sub-B,
    /// CA-B5/CA-B6, #82). Restrito a Pending/Rejected — os demais valores do enum sao
    /// transicoes de sistema (Queued/Processing/Published/Error) geridas pelos jobs, nao pelo
    /// endpoint manual. O controller ja valida o valor recebido antes de chamar este metodo;
    /// esta checagem e defesa em profundidade (nunca deve disparar via fluxo normal da API).
    /// </summary>
    public void UpdateStatusManually(ProductStatus status)
    {
        if (status != ProductStatus.Pending && status != ProductStatus.Rejected)
            throw new ArgumentException(
                "Status manual deve ser Pending ou Rejected.", nameof(status));

        Status = status;
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

    /// <summary>
    /// Marca o produto ML como aguardando importacao manual do link de afiliado (Gate 1.5, Issue
    /// #182/#184 — o endpoint affiliate-tools/links nao esta acessivel; fluxo passa a ser
    /// semi-manual: operador cola o link gerado na ferramenta oficial do ML no dashboard).
    /// </summary>
    public void MarkAsAwaitingAffiliateLink()
    {
        Status = ProductStatus.AwaitingAffiliateLink;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Preenche o AffiliateLink importado manualmente pelo operador (Issue #182/#184) e devolve o
    /// produto ao fluxo normal do ProcessorJob (Status = Queued, reprocessado na proxima execucao).
    /// </summary>
    public void ResolveAffiliateLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            throw new ArgumentException("Link nao pode ser nulo ou vazio.", nameof(link));

        AffiliateLink = link;
        Status = ProductStatus.Queued;
        UpdatedAt = DateTime.UtcNow;
    }
}
