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
/// Cobre CA-D1 a CA-D7 e CA-A9 (endpoints publicos sem autenticacao) — Issue #11 / Sub-D.
/// </summary>
public class PublicControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<Product> SeedPublishedProductAsync(
        string title = "Fone Bluetooth XYZ",
        string category = "Eletronicos",
        int aiScore = 8,
        string? mediaLocalPath = "/app/media/abc123.jpg")
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
            slug: $"fone-bluetooth-{Guid.NewGuid():N}",
            category: category,
            platform: Platform.Amazon,
            externalId: $"ext-{Guid.NewGuid():N}");

        product.UpdateAiResult(aiScore, "Otimo desconto e produto relevante", "Confira essa oferta!");
        product.MarkAsPublished();
        if (mediaLocalPath is not null)
            product.SetLocalMedia(mediaLocalPath, "image");

        db.Products.Add(product);
        await db.SaveChangesAsync(CancellationToken.None);
        return product;
    }

    [Fact]
    public async Task GetDeals_SemAuthorizationHeader_Retorna200()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync();

        var response = await client.GetAsync("/api/public/deals");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDeals_ApenasCamposAutorizados_NuncaExpoeExternalIdAiScoreAiReason()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync();

        var response = await client.GetAsync("/api/public/deals");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotContain("externalId", "ExternalId nunca pode ser exposto (CA-D2)");
        json.Should().NotContain("aiScore", "AiScore nunca pode ser exposto (CA-D2)");
        json.Should().NotContain("aiReason", "AiReason nunca pode ser exposto (CA-D2)");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];

        var allowedProperties = new[]
        {
            "title", "salePrice", "originalPrice", "discountPct", "affiliateLink",
            "mediaUrl", "mediaLocalPath", "slug", "category", "collectedAt", "platform",
        };

        foreach (var property in item.EnumerateObject())
        {
            allowedProperties.Should().Contain(property.Name,
                $"o campo '{property.Name}' nao esta na lista de campos autorizados (CA-D2)");
        }

        item.GetProperty("title").GetString().Should().Be("Fone Bluetooth XYZ");
        item.GetProperty("mediaLocalPath").GetString().Should().Contain("/media/abc123.jpg");
    }

    [Fact]
    public async Task GetDeals_ApenasProdutosPublicados_NaoRetornaOutrosStatus()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var pending = new Product(
                "Produto pendente", "desc", 10m, 20m, 50m, null,
                $"pendente-{Guid.NewGuid():N}", "Geral", Platform.Amazon, externalId: Guid.NewGuid().ToString());
            db.Products.Add(pending);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var published = await SeedPublishedProductAsync(title: "Produto publicado unico");

        var response = await client.GetAsync("/api/public/deals?pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Contain(published.Title);
        titles.Should().NotContain("Produto pendente");
    }

    [Fact]
    public async Task GetDealBySlug_SlugExistente_Retorna200ComProduto()
    {
        var client = _factory.CreateClient();
        var product = await SeedPublishedProductAsync(title: "Produto Unico Slug");

        var response = await client.GetAsync($"/api/public/deals/{product.Slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("slug").GetString().Should().Be(product.Slug);
    }

    [Fact]
    public async Task GetDealBySlug_SlugInexistente_Retorna404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/public/deals/slug-que-nao-existe-jamais");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDealsByCategory_FiltraApenasCategoriaInformada()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync(title: "Produto Categoria A", category: "CategoriaA");
        await SeedPublishedProductAsync(title: "Produto Categoria B", category: "CategoriaB");

        var response = await client.GetAsync("/api/public/deals/category/CategoriaA");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Contain("Produto Categoria A");
        titles.Should().NotContain("Produto Categoria B");
    }

    [Fact]
    public async Task GetDeals_SemParametrosDePaginacao_UsaPageUmPageSizeVinte()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync();

        var response = await client.GetAsync("/api/public/deals");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task GetDeals_PageSizeAcimaDoMaximo_TruncaPara100()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync();

        var response = await client.GetAsync("/api/public/deals?pageSize=500");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("pageSize").GetInt32().Should().Be(100);
    }
}
