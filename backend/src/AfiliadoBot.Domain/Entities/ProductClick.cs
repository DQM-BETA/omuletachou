namespace AfiliadoBot.Domain.Entities;

/// <summary>
/// Evento anonimo de clique em produto (Issue #231, design.md secao 4): tabela de eventos
/// append-only, guarda apenas produto + timestamp — nenhum dado de usuario/sessao/IP, por
/// definicao (CA 2.3). Alimenta relatorios futuros por periodo; o ranking "mais clicados" usado
/// pela faixa de sugeridos le do contador desnormalizado Product.ClickCount, nao desta tabela.
/// Sem navegacao para Product — nao necessaria para os casos de uso desta issue (mesmo principio
/// de menor superficie ja usado em JobRun).
/// </summary>
public class ProductClick
{
    public long Id { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTime ClickedAt { get; private set; }

    // Construtor para EF Core
    private ProductClick() { }

    public ProductClick(Guid productId)
    {
        ProductId = productId;
        ClickedAt = DateTime.UtcNow;
    }
}
