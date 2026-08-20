using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NpgsqlTypes;

namespace AfiliadoBot.Tests.Infrastructure;

/// <summary>
/// Issue #228 (sub-issue #242) — indice composto novo orientado a Plataforma
/// (IX_products_status_platform_createdat), design.md secao 2.2 / especificacao-tecnica.md secao 1.
/// Issue #260 (sub-issue #267) — coluna gerada search_vector (tsvector) + indice GIN
/// (design.md secao 2.2/2.4, especificacao-tecnica.md secao 1).
/// </summary>
public class ProductConfigurationTests
{
    // Usa o provider Npgsql real, com uma connection string que nunca chega a conectar (a
    // construcao/inspecao do modelo do EF nao abre conexao). Necessario desde a Issue #260/#267:
    // o shadow property SearchVector usa o tipo Npgsql-especifico NpgsqlTsVector, que o provider
    // InMemory nao sabe mapear — o ModelValidator rejeitaria o modelo inteiro (nao so essa
    // propriedade), quebrando tambem os testes do indice de Plataforma (Issue #228) que nem tocam
    // em SearchVector.
    private static AfiliadoBotDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseNpgsql("Host=localhost;Database=afiliadoBot_modelcheck;Username=test;Password=test")
            .Options;
        return new AfiliadoBotDbContext(options);
    }

    private static IIndex ObterIndicePlatform()
    {
        using var context = CriarContexto();
        // O modelo runtime (otimizado para leitura) nao guarda metadados de design-time como
        // IsDescending — precisamos do design-time model para inspecionar a configuracao completa.
        var model = context.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Product));
        entityType.Should().NotBeNull();

        var index = entityType!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "IX_products_status_platform_createdat");
        index.Should().NotBeNull(
            "o indice IX_products_status_platform_createdat deve existir na configuracao de Product (Issue #228)");
        return index!;
    }

    [Fact]
    public void Indice_StatusPlatformCreatedAt_Existe()
    {
        ObterIndicePlatform();
    }

    [Fact]
    public void Indice_StatusPlatformCreatedAt_ColunasNaOrdemCorreta()
    {
        var index = ObterIndicePlatform();

        var nomesColunas = index.Properties.Select(p => p.Name).ToArray();
        nomesColunas.Should().Equal("Status", "Platform", "CreatedAt");
    }

    [Fact]
    public void Indice_StatusPlatformCreatedAt_ApenasCreatedAtEhDescendente()
    {
        var index = ObterIndicePlatform();

        // Status e Platform ascendentes, CreatedAt descendente (design.md secao 2.2).
        index.IsDescending.Should().Equal(false, false, true);
    }

    [Fact]
    public void Indice_StatusPlatformCreatedAt_NaoEhUnico()
    {
        var index = ObterIndicePlatform();

        index.IsUnique.Should().BeFalse(
            "o indice nao deve ser UNIQUE — multiplos produtos podem compartilhar Status/Platform/CreatedAt");
    }

    // Coluna gerada search_vector (tsvector) + indice GIN (Issue #260, sub-issue #267). Espelha o
    // padrao de teste de shadow property/indice ja usado acima para
    // IX_products_status_platform_createdat (Issue #228). Estes testes validam apenas a
    // CONFIGURACAO do EF (mapeamento/metadados) — o comportamento real (coluna gerada + GIN
    // funcionando de fato, immutable_unaccent IMMUTABLE, dicionario portugues) e validado a parte
    // contra Postgres real (aplicacao da migration via `dotnet ef database update` + inspecao via
    // psql/consulta real, evidencia anexada ao PR desta sub-issue).
    private static IReadOnlyProperty ObterPropriedadeSearchVector()
    {
        using var context = CriarContexto();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Product));
        entityType.Should().NotBeNull();

        var property = entityType!.FindProperty("SearchVector");
        property.Should().NotBeNull(
            "search_vector deve estar mapeado como shadow property em Product (Issue #260/#267)");
        return property!;
    }

    private static IReadOnlyIndex ObterIndiceSearchVector()
    {
        using var context = CriarContexto();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Product));
        entityType.Should().NotBeNull();

        var index = entityType!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "IX_products_search_vector");
        index.Should().NotBeNull(
            "o indice IX_products_search_vector deve existir na configuracao de Product (Issue #260/#267)");
        return index!;
    }

    [Fact]
    public void SearchVector_MapeadaComoShadowProperty_ComTipoNpgsqlTsVector()
    {
        var property = ObterPropriedadeSearchVector();

        property.ClrType.Should().Be(typeof(NpgsqlTsVector));
        property.GetColumnName().Should().Be("search_vector");
        property.IsShadowProperty().Should().BeTrue(
            "search_vector nao deve ser exposta como propriedade CLR publica em Product " +
            "(so usada em SQL raw da query de busca, nao em C#)");
    }

    [Fact]
    public void SearchVector_ColunaGerada_ComSqlEsperadoEArmazenadaComoStored()
    {
        var property = ObterPropriedadeSearchVector();

        property.GetComputedColumnSql().Should().Be(
            "setweight(to_tsvector('portuguese', immutable_unaccent(title)), 'A') || " +
            "setweight(to_tsvector('portuguese', immutable_unaccent(category)), 'B') || " +
            "setweight(to_tsvector('portuguese', immutable_unaccent(description)), 'C')");
        property.GetIsStored().Should().BeTrue(
            "a coluna precisa ser GENERATED ALWAYS ... STORED (nao virtual) para o indice GIN " +
            "poder indexar o valor materializado");
    }

    [Fact]
    public void Indice_SearchVector_Existe_SobreAPropriedadeSearchVector()
    {
        var index = ObterIndiceSearchVector();

        index.Properties.Select(p => p.Name).Should().Equal("SearchVector");
    }

    [Fact]
    public void Indice_SearchVector_UsaMetodoGin()
    {
        var index = ObterIndiceSearchVector();

        index.GetMethod().Should().Be("gin");
    }

    [Fact]
    public void Indice_SearchVector_NaoEhUnico()
    {
        var index = ObterIndiceSearchVector();

        index.IsUnique.Should().BeFalse();
    }
}
