using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Public;

/// <summary>
/// Cobre T-03 (ISSUE-231, sub-issue #278) — GET /api/public/products/suggested.
/// Criterios de aceite em openspec/changes/issue-231-faixa-de-produtos-sugeridos/tasks.md (T-03) e
/// documentacoes/ISSUE-231-faixa-de-produtos-sugeridos/especificacao-tecnica.md §3.3.
/// </summary>
public class PublicProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<Product> SeedPublishedProductAsync(
        string title,
        string category,
        int clickCount = 0,
        DateTime? createdAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        var product = new Product(
            title: title,
            description: "Descricao do produto",
            salePrice: 99.90m,
            originalPrice: 199.90m,
            discountPct: 50m,
            affiliateLink: "https://affiliate.example.com/produto",
            slug: $"produto-{Guid.NewGuid():N}",
            category: category,
            platform: Platform.Amazon,
            externalId: $"ext-{Guid.NewGuid():N}");

        product.UpdateAiResult(8, "Otimo desconto", "Confira essa oferta!");
        product.MarkAsPublished();

        for (var i = 0; i < clickCount; i++)
            product.RegisterClick();

        db.Products.Add(product);
        await db.SaveChangesAsync();

        if (createdAtUtc.HasValue)
        {
            db.Entry(product).Property("CreatedAt").CurrentValue = createdAtUtc.Value;
            await db.SaveChangesAsync();
        }

        return product;
    }

    private async Task SeedPendingProductAsync(string title, string category)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        var product = new Product(
            title, "desc", 10m, 20m, 50m, null,
            $"pendente-{Guid.NewGuid():N}", category, Platform.Amazon,
            externalId: $"ext-{Guid.NewGuid():N}");

        db.Products.Add(product);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSuggested_CategoriaComQuatroOuMaisProdutosEHasResultsTrue_RetornaOrdenadoPorClickCountDesc()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-suggested-{Guid.NewGuid():N}";

        await SeedPublishedProductAsync("Pouco clicado", categoria, clickCount: 1);
        await SeedPublishedProductAsync("Muito clicado", categoria, clickCount: 10);
        await SeedPublishedProductAsync("Medio clicado", categoria, clickCount: 5);
        await SeedPublishedProductAsync("Sem clique", categoria, clickCount: 0);

        var response = await client.GetAsync(
            $"/api/public/products/suggested?categories={categoria}&hasResults=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var titles = body.EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();

        titles.Should().Equal("Muito clicado", "Medio clicado", "Pouco clicado", "Sem clique");
    }

    [Fact]
    public async Task GetSuggested_CategoriaComQuatroOuMaisProdutos_CadaItemIncluiId()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-suggested-id-{Guid.NewGuid():N}";

        var p1 = await SeedPublishedProductAsync("Produto 1", categoria, clickCount: 4);
        await SeedPublishedProductAsync("Produto 2", categoria, clickCount: 3);
        await SeedPublishedProductAsync("Produto 3", categoria, clickCount: 2);
        await SeedPublishedProductAsync("Produto 4", categoria, clickCount: 1);

        var response = await client.GetAsync(
            $"/api/public/products/suggested?categories={categoria}&hasResults=true");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var first = body.EnumerateArray().First();
        first.GetProperty("id").GetGuid().Should().Be(p1.Id);
    }

    [Fact]
    public async Task GetSuggested_HasResultsFalse_RetornaFallbackGeral_IgnorandoCategoria()
    {
        var client = _factory.CreateClient();
        var categoriaFiltrada = $"cat-filtrada-{Guid.NewGuid():N}";
        var categoriaOutra = $"cat-outra-{Guid.NewGuid():N}";

        // 4 produtos fora da categoria filtrada — fallback deve trazer estes (mais clicados geral).
        await SeedPublishedProductAsync("Fallback 1", categoriaOutra, clickCount: 20);
        await SeedPublishedProductAsync("Fallback 2", categoriaOutra, clickCount: 15);
        await SeedPublishedProductAsync("Fallback 3", categoriaOutra, clickCount: 10);
        await SeedPublishedProductAsync("Fallback 4", categoriaOutra, clickCount: 5);
        // Produto dentro da categoria filtrada, mas hasResults=false deve ignora-la e trazer o
        // conjunto geral (ordenado por click_count) em vez desta unica categoria.
        await SeedPublishedProductAsync("Categoria filtrada vazia", categoriaFiltrada, clickCount: 1);

        var response = await client.GetAsync(
            $"/api/public/products/suggested?categories={categoriaFiltrada}&hasResults=false");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();

        titles.Should().Contain("Fallback 1");
        titles.Should().Contain("Fallback 2");
        titles.Should().Contain("Fallback 3");
        titles.Should().Contain("Fallback 4");
    }

    [Fact]
    public async Task GetSuggested_CategoriesAusente_RetornaFallbackGeral()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-sem-filtro-{Guid.NewGuid():N}";

        await SeedPublishedProductAsync("Geral 1", categoria, clickCount: 9);
        await SeedPublishedProductAsync("Geral 2", categoria, clickCount: 8);
        await SeedPublishedProductAsync("Geral 3", categoria, clickCount: 7);
        await SeedPublishedProductAsync("Geral 4", categoria, clickCount: 6);

        var response = await client.GetAsync("/api/public/products/suggested?hasResults=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task GetSuggested_ListaResultanteComMenosDeQuatroProdutos_RetornaListaVazia()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-poucos-{Guid.NewGuid():N}";

        await SeedPublishedProductAsync("Unico produto", categoria, clickCount: 5);
        await SeedPublishedProductAsync("Segundo produto", categoria, clickCount: 3);

        var response = await client.GetAsync(
            $"/api/public/products/suggested?categories={categoria}&hasResults=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetSuggested_NenhumProdutoComClickCountMaiorQueZero_AindaAssimRetornaProdutos_DesempatandoPorCreatedAtDesc()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-sem-clique-{Guid.NewGuid():N}";
        var agora = DateTime.UtcNow;

        var p1 = await SeedPublishedProductAsync("Mais antigo", categoria, createdAtUtc: agora.AddDays(-5));
        var p2 = await SeedPublishedProductAsync("Mais recente", categoria, createdAtUtc: agora);
        var p3 = await SeedPublishedProductAsync("Intermediario 1", categoria, createdAtUtc: agora.AddDays(-2));
        var p4 = await SeedPublishedProductAsync("Intermediario 2", categoria, createdAtUtc: agora.AddDays(-3));

        var response = await client.GetAsync(
            $"/api/public/products/suggested?categories={categoria}&hasResults=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var titles = body.EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();

        titles.Should().Equal("Mais recente", "Intermediario 1", "Intermediario 2", "Mais antigo");
    }

    [Fact]
    public async Task GetSuggested_ApenasProdutosPublicados_IgnoraOutrosStatus()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-status-{Guid.NewGuid():N}";

        await SeedPublishedProductAsync("Publicado 1", categoria, clickCount: 4);
        await SeedPublishedProductAsync("Publicado 2", categoria, clickCount: 3);
        await SeedPublishedProductAsync("Publicado 3", categoria, clickCount: 2);
        await SeedPublishedProductAsync("Publicado 4", categoria, clickCount: 1);
        await SeedPendingProductAsync("Pendente", categoria);

        var response = await client.GetAsync(
            $"/api/public/products/suggested?categories={categoria}&hasResults=true");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();
        titles.Should().NotContain("Pendente");
    }

    [Fact]
    public async Task GetSuggested_LimiteDeDezProdutos_RespeitaLimit()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-limite-{Guid.NewGuid():N}";

        for (var i = 0; i < 15; i++)
            await SeedPublishedProductAsync($"Produto {i}", categoria, clickCount: i);

        var response = await client.GetAsync(
            $"/api/public/products/suggested?categories={categoria}&hasResults=true");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetArrayLength().Should().Be(10);
    }
}
