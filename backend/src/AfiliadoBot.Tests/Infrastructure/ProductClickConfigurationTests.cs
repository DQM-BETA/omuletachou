using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AfiliadoBot.Tests.Infrastructure;

/// <summary>
/// Issue #231 (sub-issue #276) — mapeamento da tabela product_clicks (tabela de eventos,
/// append-only, design.md secao 4). Espelha o padrao de teste de configuracao ja usado em
/// ProductConfigurationTests (design-time model, sem abrir conexao real).
/// </summary>
public class ProductClickConfigurationTests
{
    private static AfiliadoBotDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseNpgsql("Host=localhost;Database=afiliadoBot_modelcheck;Username=test;Password=test")
            .Options;
        return new AfiliadoBotDbContext(options);
    }

    private static IEntityType ObterEntidade()
    {
        using var context = CriarContexto();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(ProductClick));
        entityType.Should().NotBeNull("ProductClick deve estar mapeado no modelo do EF (Issue #231)");
        return (IEntityType)entityType!;
    }

    [Fact]
    public void MapeiaParaTabela_product_clicks()
    {
        var entityType = ObterEntidade();
        entityType.GetTableName().Should().Be("product_clicks");
    }

    [Fact]
    public void Id_MapeadoComoChavePrimaria_ValueGeneratedOnAdd()
    {
        var entityType = ObterEntidade();
        var pk = entityType.FindPrimaryKey();

        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().Equal(nameof(ProductClick.Id));

        var idProperty = entityType.FindProperty(nameof(ProductClick.Id))!;
        idProperty.GetColumnName().Should().Be("id");
        idProperty.ValueGenerated.Should().Be(ValueGenerated.OnAdd);
    }

    [Fact]
    public void ProductId_MapeadoComoColunaProductId_Obrigatorio()
    {
        var entityType = ObterEntidade();
        var property = entityType.FindProperty(nameof(ProductClick.ProductId))!;

        property.Should().NotBeNull();
        property.GetColumnName().Should().Be("product_id");
        property.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ClickedAt_MapeadoComoTimestamptz_Obrigatorio()
    {
        var entityType = ObterEntidade();
        var property = entityType.FindProperty(nameof(ProductClick.ClickedAt))!;

        property.Should().NotBeNull();
        property.GetColumnName().Should().Be("clicked_at");
        property.GetColumnType().Should().Be("timestamptz");
        property.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Indice_ProductId_Existe_NaoEhUnico()
    {
        var entityType = ObterEntidade();
        var index = entityType.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "IX_product_clicks_product_id");

        index.Should().NotBeNull("o indice IX_product_clicks_product_id deve existir (Issue #231)");
        index!.Properties.Select(p => p.Name).Should().Equal(nameof(ProductClick.ProductId));
        index.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void ForeignKey_ProductId_ReferenciaProducts_ComOnDeleteCascade()
    {
        var entityType = ObterEntidade();
        var fk = entityType.GetForeignKeys().SingleOrDefault();

        fk.Should().NotBeNull("ProductClick deve ter FK para Product (Issue #231)");
        fk!.PrincipalEntityType.ClrType.Should().Be(typeof(Product));
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void NaoTemNavegacaoParaProduct()
    {
        // design.md/especificacao-tecnica.md secao 2.2: sem navegacao — evita FK object
        // navigation desnecessaria (mesmo padrao de JobRun).
        var entityType = ObterEntidade();
        entityType.ClrType.GetProperty("Product").Should().BeNull();
    }
}
