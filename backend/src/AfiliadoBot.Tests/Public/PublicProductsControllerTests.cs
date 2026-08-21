using System.Net;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Public;

/// <summary>
/// Cobre CA 2.1-2.4 (Issue #231, sub-issue #277/T-02) — endpoint publico de registro de clique
/// (POST /api/public/products/{id}/click), especificacao-tecnica.md secao 3.2.
/// </summary>
public class PublicProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<Product> SeedProductAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        var product = new Product(
            title: "Fone Bluetooth XYZ",
            description: "Descricao do produto",
            salePrice: 99.90m,
            originalPrice: 199.90m,
            discountPct: 50m,
            affiliateLink: "https://affiliate.example.com/produto",
            slug: $"fone-bluetooth-{Guid.NewGuid():N}",
            category: "Eletronicos",
            platform: Platform.Amazon,
            externalId: $"ext-{Guid.NewGuid():N}");

        product.MarkAsPublished();

        db.Products.Add(product);
        await db.SaveChangesAsync(CancellationToken.None);

        return product;
    }

    [Fact]
    public async Task RegisterClick_ProdutoExistente_Retorna202EPersisteEventoEIncrementaContador()
    {
        var product = await SeedProductAsync();
        var client = _factory.CreateClient();
        var before = DateTime.UtcNow;

        var response = await client.PostAsync($"/api/public/products/{product.Id}/click", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        var click = await verifyDb.ProductClicks.FirstOrDefaultAsync(c => c.ProductId == product.Id);
        click.Should().NotBeNull();
        click!.ClickedAt.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));

        var updatedProduct = await verifyDb.Products.FirstAsync(p => p.Id == product.Id);
        updatedProduct.ClickCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterClick_MultiplasChamadas_IncrementaContadorPorChamada()
    {
        var product = await SeedProductAsync();
        var client = _factory.CreateClient();

        // 2 chamadas ficam bem dentro do limite da policy public-write (10/min) — sem necessidade
        // de espaçar as chamadas.
        await client.PostAsync($"/api/public/products/{product.Id}/click", content: null);
        await client.PostAsync($"/api/public/products/{product.Id}/click", content: null);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        var updatedProduct = await verifyDb.Products.FirstAsync(p => p.Id == product.Id);
        updatedProduct.ClickCount.Should().Be(2);

        var clickCount = await verifyDb.ProductClicks.CountAsync(c => c.ProductId == product.Id);
        clickCount.Should().Be(2);
    }

    [Fact]
    public async Task RegisterClick_ProdutoInexistente_Retorna202SemCriarEvento()
    {
        // CA 2.2: nunca 404 — o sendBeacon do frontend nao le a resposta.
        var client = _factory.CreateClient();
        var idInexistente = Guid.NewGuid();

        var response = await client.PostAsync($"/api/public/products/{idInexistente}/click", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        (await verifyDb.ProductClicks.AnyAsync(c => c.ProductId == idInexistente)).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterClick_EventoPersistido_ContemApenasIdProductIdEClickedAt()
    {
        // CA 2.3: evento anonimo — sem dado de usuario/sessao/IP persistido. Validacao por reflexao
        // das propriedades publicas da entidade (garante que nenhum campo novo de identificacao foi
        // adicionado sem atualizar este teste/a especificacao).
        var product = await SeedProductAsync();
        var client = _factory.CreateClient();

        await client.PostAsync($"/api/public/products/{product.Id}/click", content: null);

        var properties = typeof(ProductClick).GetProperties().Select(p => p.Name).ToArray();
        properties.Should().BeEquivalentTo(new[] { "Id", "ProductId", "ClickedAt" });
    }

    [Fact]
    public async Task RegisterClick_SemCorpo_NaoExigeContentType()
    {
        // Decisao 4 do design.md: sem [FromBody], compativel com navigator.sendBeacon(url) que nao
        // envia payload nenhum.
        var product = await SeedProductAsync();
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/public/products/{product.Id}/click");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
