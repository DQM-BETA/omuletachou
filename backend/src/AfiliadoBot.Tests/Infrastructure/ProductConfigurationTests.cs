using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AfiliadoBot.Tests.Infrastructure;

/// <summary>
/// Issue #228 (sub-issue #242) — indice composto novo orientado a Plataforma
/// (IX_products_status_platform_createdat), design.md secao 2.2 / especificacao-tecnica.md secao 1.
/// </summary>
public class ProductConfigurationTests
{
    private static AfiliadoBotDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
}
