using AfiliadoBot.Api.Common;
using AfiliadoBot.Api.Controllers;
using AfiliadoBot.Api.Public;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AfiliadoBot.Tests.Public;

/// <summary>
/// Fixture compartilhada por classe (Postgres real via Testcontainers — precedente
/// <c>ClaudeBudgetServiceIntegrationTests</c>), necessaria porque o provider InMemory usado por
/// <c>CustomWebApplicationFactory</c>/<c>PublicControllerTests</c> nao suporta
/// <c>tsvector</c>/<c>pg_trgm</c> (Issue #260, sub-issue #268 — especificacao-tecnica.md §4).
/// </summary>
public class PostgresSearchFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16.14-alpine")
        .WithDatabase("afiliadoBot_search_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public AfiliadoBotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }
}

/// <summary>
/// Issue #260 (sub-issue #268) — busca textual em 2 estagios de <c>PublicController.GetDeals</c>
/// (design.md §2.1-§2.6/§3, especificacao-tecnica.md §2/§3). Chama <c>GetDeals</c> direto (sem
/// <c>WebApplicationFactory</c>/HTTP — opcao "(b)" recomendada em especificacao-tecnica.md §4)
/// contra um <c>AfiliadoBotDbContext</c> apontado para o Postgres real da fixture, exercitando o
/// <c>ProductSearchService</c> por baixo com <c>tsvector</c>/<c>pg_trgm</c> de verdade.
/// Isolamento por categoria unica (Guid) por teste, mesmo padrao de <c>PublicControllerTests</c>
/// (banco compartilhado por toda a classe via <c>IClassFixture</c>).
/// </summary>
public class PublicSearchTests : IClassFixture<PostgresSearchFixture>
{
    private readonly PostgresSearchFixture _fixture;

    public PublicSearchTests(PostgresSearchFixture fixture)
    {
        _fixture = fixture;
    }

    private static Product BuildPublishedProduct(
        string title, string category, string description, decimal salePrice = 100m)
    {
        var product = new Product(
            title: title,
            description: description,
            salePrice: salePrice,
            originalPrice: salePrice * 2,
            discountPct: 50m,
            affiliateLink: "https://affiliate.example.com/produto",
            slug: $"produto-{Guid.NewGuid():N}",
            category: category,
            platform: Platform.Amazon,
            externalId: $"ext-{Guid.NewGuid():N}");

        product.UpdateAiResult(8, "Otimo produto", "Confira!");
        product.MarkAsPublished();
        return product;
    }

    private async Task<Product> SeedAsync(
        string title, string category, string description, decimal salePrice = 100m)
    {
        using var db = _fixture.CreateContext();
        var product = BuildPublishedProduct(title, category, description, salePrice);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static PublicController CreateController(AfiliadoBotDbContext db) =>
        new(db, new ProductSearchService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private static PagedResult<PublicDealDto> Unwrap(ActionResult<PagedResult<PublicDealDto>> result)
    {
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeOfType<PagedResult<PublicDealDto>>().Subject;
    }

    // CA 1.2 (nao-regressao): q ausente -> comportamento identico ao atual, IsApproximateSearch == null.
    [Fact]
    public async Task GetDeals_QAusente_ComportamentoAtual_IsApproximateSearchNulo()
    {
        var categoria = $"cat-q-ausente-{Guid.NewGuid():N}";
        await SeedAsync("Produto Generico", categoria, "Descricao qualquer");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null, q: null,
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().ContainSingle();
        paged.IsApproximateSearch.Should().BeNull();
    }

    // CA E.1: q com 1 caractere -> tratado como ausente, sem erro.
    [Fact]
    public async Task GetDeals_QComUmCaractere_TratadoComoAusente_SemErro()
    {
        var categoria = $"cat-q-curto-{Guid.NewGuid():N}";
        await SeedAsync("Produto Qualquer", categoria, "Descricao");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null, q: "a",
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.IsApproximateSearch.Should().BeNull();
        paged.Items.Should().ContainSingle("q de 1 caractere deve ser tratado como ausente (CA E.1)");
    }

    // CA 3.1: match apenas em description -> produto aparece.
    [Fact]
    public async Task GetDeals_MatchApenasNaDescricao_ProdutoAparece_CA31()
    {
        var categoria = $"cat-match-descricao-{Guid.NewGuid():N}";
        var termoUnico = $"giroscopiozeta{Guid.NewGuid():N}"[..30];
        await SeedAsync(
            "Produto Sem Relacao No Titulo", categoria,
            $"Este produto vem com sensor {termoUnico} embutido para maior precisao");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null, q: termoUnico,
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().ContainSingle();
        paged.IsApproximateSearch.Should().BeFalse("match lexico exato deve vir do estagio 1");
    }

    // CA 3.2/3.3: match em titulo rankeia acima de match so em descricao; produto com match em
    // 2+ campos rankeia acima de produto com match em 1 so.
    [Fact]
    public async Task GetDeals_MatchEmTitulo_RankeiaAcimaDeMatchSoEmDescricao_CA32()
    {
        var categoria = $"cat-rank-titulo-{Guid.NewGuid():N}";
        var termo = $"zetacron{Guid.NewGuid():N}"[..20];
        await SeedAsync($"Produto {termo} Especial", categoria, "Descricao sem relacao nenhuma");
        await SeedAsync("Produto Base Generico", categoria, $"Descricao contendo {termo} no meio do texto");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null, q: termo,
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().HaveCount(2);
        paged.Items[0].Title.Should().Contain(termo, "match em titulo (peso A) rankeia primeiro");
    }

    [Fact]
    public async Task GetDeals_MatchEmMultiplosCampos_RankeiaAcimaDeMatchEmUmSoCampo_CA33()
    {
        var categoria = $"cat-rank-multicampo-{Guid.NewGuid():N}";
        var termo = $"omegaflux{Guid.NewGuid():N}"[..20];
        await SeedAsync(
            $"Produto {termo}", categoria, $"Descricao tambem menciona {termo} novamente");
        await SeedAsync("Produto Isolado", categoria, $"Descricao contendo apenas {termo} uma vez");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null, q: termo,
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().HaveCount(2);
        paged.Items[0].Title.Should().Be($"Produto {termo}",
            "produto com match em titulo+descricao deve rankear acima do match so em descricao");
    }

    // Plural/singular e variacao de acento -> resolvido pelo estagio 1 (stemmer + immutable_unaccent).
    [Fact]
    public async Task GetDeals_VariacaoDeAcento_ResolvidoPeloEstagio1_IsApproximateSearchFalse()
    {
        var categoria = $"cat-acento-{Guid.NewGuid():N}";
        await SeedAsync("Tênis Esportivo Confortável", categoria, "Ótimo para corrida");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null, q: "tenis",
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().ContainSingle();
        paged.IsApproximateSearch.Should().BeFalse(
            "unaccent+stemmer do dicionario portugues resolve 'tenis' -> 'tênis' no estagio 1, sem precisar do fallback");
    }

    // CA 4.1/4.3: termo com erro de digitacao sem match exato -> estagio 2 aciona,
    // IsApproximateSearch == true.
    [Fact]
    public async Task GetDeals_ErroDeDigitacao_AcionaEstagio2_IsApproximateSearchTrue_CA41()
    {
        var categoria = $"cat-typo-{Guid.NewGuid():N}";
        await SeedAsync("Fone Bluetooth JBL Original", categoria, "Som de alta qualidade sem fio");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        // "Bluetoth" (falta um 'o') nao existe como lexema — tsquery do estagio 1 nao casa.
        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null, q: "Bluetoth",
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().ContainSingle("similarity() do fallback deve achar 'Bluetooth' mesmo com o typo");
        paged.IsApproximateSearch.Should().BeTrue();
    }

    // CA 5.1: termo sem nenhuma relacao (abaixo do threshold 0.15 em tudo) -> lista vazia,
    // IsApproximateSearch == false (vazio genuino, distinto do fallback com resultado).
    [Fact]
    public async Task GetDeals_TermoSemNenhumaRelacao_VazioGenuino_CA51()
    {
        var categoria = $"cat-vazio-genuino-{Guid.NewGuid():N}";
        await SeedAsync("Panela de Pressao Eletrica", categoria, "Ideal para o dia a dia na cozinha");

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: null,
            q: "xkcvbnqwzy9988zztop",
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().BeEmpty();
        paged.IsApproximateSearch.Should().BeFalse(
            "vazio genuino (CA 5.1) usa IsApproximateSearch=false, distinto do fallback com resultado (true)");
    }

    // CA 6.1: q combinado com category/minPrice/maxPrice -> interseccao AND.
    [Fact]
    public async Task GetDeals_QCombinadoComFiltrosExistentes_InterseccaoAnd_CA61()
    {
        var categoriaAlvo = $"cat-combo-alvo-{Guid.NewGuid():N}";
        var categoriaOutra = $"cat-combo-outra-{Guid.NewGuid():N}";
        var termo = $"nimbustech{Guid.NewGuid():N}"[..20];

        await SeedAsync($"Produto {termo} Categoria Alvo Caro", categoriaAlvo, "desc", salePrice: 900m);
        await SeedAsync($"Produto {termo} Categoria Alvo Barato", categoriaAlvo, "desc", salePrice: 150m);
        await SeedAsync($"Produto {termo} Categoria Errada", categoriaOutra, "desc", salePrice: 150m);

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoriaAlvo, subcategory: null,
            minPrice: 100m, maxPrice: 500m, minDiscount: null, sort: null, q: termo,
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().ContainSingle();
        paged.Items[0].Title.Should().Contain("Categoria Alvo Barato");
    }

    // q presente + sort=price_asc -> ordenacao por relevancia prevalece, nao por preco.
    [Fact]
    public async Task GetDeals_QPresenteComSortPriceAsc_OrdenacaoPorRelevanciaPrevalece()
    {
        var categoria = $"cat-sort-ignorado-{Guid.NewGuid():N}";
        var termo = $"quantumdrive{Guid.NewGuid():N}"[..20];

        // Match no titulo (peso A), mais caro.
        await SeedAsync($"Produto {termo} Titulo", categoria, "descricao sem relacao", salePrice: 900m);
        // Match so na categoria/descricao (peso B/C), mais barato — se sort=price_asc fosse
        // respeitado, este viria primeiro.
        await SeedAsync("Produto Base", categoria, $"descricao menciona {termo} de leve", salePrice: 10m);

        using var db = _fixture.CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetDeals(
            page: null, pageSize: null, category: categoria, subcategory: null,
            minPrice: null, maxPrice: null, minDiscount: null, sort: "price_asc", q: termo,
            ct: CancellationToken.None);

        var paged = Unwrap(result);
        paged.Items.Should().HaveCount(2);
        paged.Items[0].Title.Should().Contain("Titulo",
            "com q presente, sort=price_asc deve ser ignorado — ranking de relevancia prevalece");
    }

    // CA 7.1: nenhuma chamada a Anthropic/Claude e disparada pelo caminho de busca — evidencia
    // automatizada: nenhuma dependencia injetada em PublicController/ProductSearchService
    // referencia tipos do namespace Anthropic (revisao de codigo como evidencia complementar,
    // especificacao-tecnica.md §4).
    [Fact]
    public void PublicController_NenhumaDependenciaReferenciaAnthropicOuClaude_CA71()
    {
        var ctorParams = typeof(PublicController)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.FullName ?? p.ParameterType.Name);

        ctorParams.Should().NotContain(name =>
            name.Contains("Anthropic", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Claude", StringComparison.OrdinalIgnoreCase));
    }
}
