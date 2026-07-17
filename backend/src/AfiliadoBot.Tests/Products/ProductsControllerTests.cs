using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Products;

/// <summary>
/// Cobre CA-B1 a CA-B4 e CA-B11 (Issue #11 / Sub-B, #82) para ProductsController: paginacao
/// default/customizada/truncamento, filtros status/platform, detalhe com ai_score/ai_reason,
/// 404 e 401.
/// </summary>
public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SeededEmail = "operador-produtos@omuletachou.com.br";
    private const string SeededPassword = "SenhaForte#2026";

    private readonly CustomWebApplicationFactory _factory;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        if (!await db.Users.AnyAsync(u => u.Email == SeededEmail))
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(SeededPassword, workFactor: 12);
            db.Users.Add(new User(SeededEmail, hash));
            await db.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return loginBody.GetProperty("token").GetString()!;
    }

    private static Product NewProduct(string title, Platform platform, string category = "Geral", string? affiliateLink = "https://exemplo.com/aff")
    {
        return new Product(
            title: title,
            description: "Descricao de " + title,
            salePrice: 99.90m,
            originalPrice: 199.90m,
            discountPct: 50m,
            affiliateLink: affiliateLink,
            slug: title.ToLowerInvariant().Replace(' ', '-') + "-" + Guid.NewGuid().ToString("N")[..8],
            category: category,
            platform: platform,
            externalId: Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task GetProducts_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProducts_SemParametros_Retorna200ComPaginacaoDefault()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(NewProduct("Produto Default A", Platform.Amazon));
            db.Products.Add(NewProduct("Produto Default B", Platform.Shopee));
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetProducts_ComPageSizeAcimaDoMaximo_TruncaPara100()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/products?pageSize=500");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pageSize").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task GetProducts_ComPageCustomizada_RetornaPageInformada()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/products?page=2&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("page").GetInt32().Should().Be(2);
        body.GetProperty("pageSize").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task GetProducts_FiltroStatusEPlatform_RetornaApenasCorrespondentes()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var marker = Guid.NewGuid().ToString("N")[..8];
        var matching = NewProduct($"Produto Filtro Match {marker}", Platform.Amazon);
        var wrongPlatform = NewProduct($"Produto Filtro Wrong Platform {marker}", Platform.Shopee);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(matching);
            db.Products.Add(wrongPlatform);
            await db.SaveChangesAsync();
        }

        // Ambos os produtos nascem com Status = Pending (construtor de Product) — filtro exercita
        // "pending" (lowercase, CA-B2 usa esse formato na query string) + platform=amazon.
        var response = await client.GetAsync("/api/products?status=pending&platform=amazon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        items.Should().Contain(i => i.GetProperty("id").GetGuid() == matching.Id);
        items.Should().NotContain(i => i.GetProperty("id").GetGuid() == wrongPlatform.Id);
    }

    [Fact]
    public async Task GetProduct_Existente_RetornaDetalheComAiScoreEAiReason()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var product = NewProduct("Produto Com Score", Platform.MercadoLivre);
        product.UpdateAiResult(8, "Otimo desconto e boa avaliacao.", "Confira essa oferta!");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/products/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ai_score").GetInt32().Should().Be(8);
        body.GetProperty("ai_reason").GetString().Should().Be("Otimo desconto e boa avaliacao.");
    }

    [Fact]
    public async Task GetProduct_Inexistente_Retorna404()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProduct_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
