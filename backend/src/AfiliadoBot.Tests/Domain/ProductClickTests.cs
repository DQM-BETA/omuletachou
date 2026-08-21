using AfiliadoBot.Domain.Entities;
using FluentAssertions;

namespace AfiliadoBot.Tests.Domain;

/// <summary>
/// Issue #231 (sub-issue #276) — evento anonimo de clique em produto (tabela de eventos
/// append-only, design.md secao 4). Sem dado de usuario/sessao/IP — apenas ProductId + ClickedAt.
/// </summary>
public class ProductClickTests
{
    [Fact]
    public void Constructor_SetaProductId()
    {
        var productId = Guid.NewGuid();
        var click = new ProductClick(productId);

        click.ProductId.Should().Be(productId);
    }

    [Fact]
    public void Constructor_SetaClickedAtProximoDeAgora()
    {
        var antes = DateTime.UtcNow;
        var click = new ProductClick(Guid.NewGuid());
        var depois = DateTime.UtcNow;

        click.ClickedAt.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }
}
