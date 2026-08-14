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
        string? subcategory = null,
        decimal salePrice = 99.90m,
        decimal originalPrice = 199.90m,
        decimal discountPct = 50m,
        int aiScore = 8,
        DateTime? createdAtUtc = null,
        string? mediaLocalPath = "/app/media/abc123.jpg")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        var product = new Product(
            title: title,
            description: "Descricao do produto",
            salePrice: salePrice,
            originalPrice: originalPrice,
            discountPct: discountPct,
            affiliateLink: "https://affiliate.example.com/produto",
            slug: $"fone-bluetooth-{Guid.NewGuid():N}",
            category: category,
            platform: Platform.Amazon,
            externalId: $"ext-{Guid.NewGuid():N}",
            subcategory: subcategory);

        product.UpdateAiResult(aiScore, "Otimo desconto e produto relevante", "Confira essa oferta!");
        product.MarkAsPublished();
        if (mediaLocalPath is not null)
            product.SetLocalMedia(mediaLocalPath, "image");

        db.Products.Add(product);
        await db.SaveChangesAsync(CancellationToken.None);

        if (createdAtUtc.HasValue)
        {
            // CreatedAt e setado no construtor (DateTime.UtcNow); para testar sort=recent
            // precisamos de valores distintos e controlados — ajusta via SQL direto no
            // DbContext apos o insert inicial (sem expor um setter publico na entidade so
            // para teste).
            db.Entry(product).Property("CreatedAt").CurrentValue = createdAtUtc.Value;
            await db.SaveChangesAsync(CancellationToken.None);
        }

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
        // Filtra por uma categoria unica (Guid) para isolar do restante da suite — a factory e
        // IClassFixture (banco InMemory compartilhado por todos os testes da classe), entao
        // "items[0]" sem filtro nao garantiria pegar o produto semeado por este teste.
        var client = _factory.CreateClient();
        var categoria = $"cat-campos-autorizados-{Guid.NewGuid():N}";
        await SeedPublishedProductAsync(category: categoria);

        var response = await client.GetAsync($"/api/public/deals?category={categoria}");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotContain("externalId", "ExternalId nunca pode ser exposto (CA-D2)");
        json.Should().NotContain("aiScore", "AiScore nunca pode ser exposto (CA-D2)");
        json.Should().NotContain("aiReason", "AiReason nunca pode ser exposto (CA-D2)");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];

        var allowedProperties = new[]
        {
            "title", "salePrice", "originalPrice", "discountPct", "affiliateLink",
            "mediaUrl", "mediaLocalPath", "slug", "category", "subcategory", "collectedAt",
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
    public async Task GetDeals_JsonDeResposta_NuncaContemCampoPlatform()
    {
        // CA 5.1 — assert negativo explicito: "platform" (ou equivalente) nao pode aparecer no
        // contrato publico, mesmo que "Platform" continue existindo na entidade Product.
        var client = _factory.CreateClient();
        var seeded = await SeedPublishedProductAsync();

        var response = await client.GetAsync("/api/public/deals");
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("platform", "Platform foi removido do contrato publico (CA 5.1)");

        var bySlugResponse = await client.GetAsync($"/api/public/deals/{seeded.Slug}");
        var bySlugJson = await bySlugResponse.Content.ReadAsStringAsync();
        bySlugJson.Should().NotContain("platform", "GetBySlug tambem usa PublicDealDto (CA 5.1)");
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
    public async Task GetDealsRotaAntigaDeCategoria_NaoExisteMais_Retorna404()
    {
        // Issue #167 (design.md secao 5.2): GetByCategory (/deals/category/{categoria}) foi
        // removida — o caso e coberto por GetDeals?category=... agora (teste abaixo).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/public/deals/category/QualquerCategoria");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDeals_FiltroPorCategory_RetornaApenasCategoriaInformada()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync(title: "Produto Categoria A", category: "CategoriaA");
        await SeedPublishedProductAsync(title: "Produto Categoria B", category: "CategoriaB");

        var response = await client.GetAsync("/api/public/deals?category=CategoriaA");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Contain("Produto Categoria A");
        titles.Should().NotContain("Produto Categoria B");
    }

    [Fact]
    public async Task GetDeals_FiltroPorCategoryESubcategory_Combinados()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync(
            title: "Celular X", category: "Eletrônicos", subcategory: "Celulares e Smartphones");
        await SeedPublishedProductAsync(
            title: "Fone Y", category: "Eletrônicos", subcategory: "Áudio");
        await SeedPublishedProductAsync(
            title: "Panela Z", category: "Casa e Cozinha", subcategory: "Panelas e Utensílios");

        var response = await client.GetAsync(
            "/api/public/deals?category=Eletrônicos&subcategory=Celulares e Smartphones");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().ContainSingle().Which.Should().Be("Celular X");
    }

    [Fact]
    public async Task GetDeals_FiltroPorFaixaDePrecoEDescontoMinimo_Combinados()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync(
            title: "Dentro da faixa", salePrice: 300m, discountPct: 40m);
        await SeedPublishedProductAsync(
            title: "Preco abaixo do minimo", salePrice: 50m, discountPct: 40m);
        await SeedPublishedProductAsync(
            title: "Preco acima do maximo", salePrice: 800m, discountPct: 40m);
        await SeedPublishedProductAsync(
            title: "Desconto abaixo do minimo", salePrice: 300m, discountPct: 10m);

        var response = await client.GetAsync(
            "/api/public/deals?minPrice=100&maxPrice=500&minDiscount=30");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().ContainSingle().Which.Should().Be("Dentro da faixa");
    }

    // Os testes de ordenacao/CA6.5-6.6 abaixo isolam os dados por uma categoria unica por teste
    // (Guid) e filtram por ela na querystring: a factory e IClassFixture (uma unica instancia de
    // banco InMemory compartilhada por TODOS os testes desta classe), entao comparar a lista
    // completa sem isolamento pegaria produtos semeados por outros testes da suite.

    [Fact]
    public async Task GetDeals_SortPriceAsc_OrdenaPorPrecoCrescente()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-price-asc-{Guid.NewGuid():N}";
        await SeedPublishedProductAsync(title: "Caro", category: categoria, salePrice: 300m, aiScore: 6);
        await SeedPublishedProductAsync(title: "Barato", category: categoria, salePrice: 50m, aiScore: 9);
        await SeedPublishedProductAsync(title: "Medio", category: categoria, salePrice: 150m, aiScore: 7);

        var response = await client.GetAsync($"/api/public/deals?category={categoria}&sort=price_asc");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Equal("Barato", "Medio", "Caro");
    }

    [Fact]
    public async Task GetDeals_SortDiscountDesc_OrdenaPorDescontoDecrescente()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-discount-desc-{Guid.NewGuid():N}";
        await SeedPublishedProductAsync(title: "Desconto baixo", category: categoria, discountPct: 10m, aiScore: 9);
        await SeedPublishedProductAsync(title: "Desconto alto", category: categoria, discountPct: 70m, aiScore: 6);
        await SeedPublishedProductAsync(title: "Desconto medio", category: categoria, discountPct: 40m, aiScore: 7);

        var response = await client.GetAsync($"/api/public/deals?category={categoria}&sort=discount_desc");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Equal("Desconto alto", "Desconto medio", "Desconto baixo");
    }

    [Fact]
    public async Task GetDeals_SortRecent_OrdenaPorMaisRecente()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-recent-{Guid.NewGuid():N}";
        var agora = DateTime.UtcNow;
        await SeedPublishedProductAsync(
            title: "Mais antigo", category: categoria, aiScore: 9, createdAtUtc: agora.AddDays(-5));
        await SeedPublishedProductAsync(
            title: "Mais recente", category: categoria, aiScore: 6, createdAtUtc: agora);
        await SeedPublishedProductAsync(
            title: "Intermediario", category: categoria, aiScore: 7, createdAtUtc: agora.AddDays(-2));

        var response = await client.GetAsync($"/api/public/deals?category={categoria}&sort=recent");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Equal("Mais recente", "Intermediario", "Mais antigo");
    }

    [Fact]
    public async Task GetDeals_SemSortInformado_OrdenaPorAiScoreDescendente_CA65()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-default-sort-{Guid.NewGuid():N}";
        await SeedPublishedProductAsync(title: "Score baixo", category: categoria, aiScore: 6);
        await SeedPublishedProductAsync(title: "Score alto", category: categoria, aiScore: 10);
        await SeedPublishedProductAsync(title: "Score medio", category: categoria, aiScore: 8);

        var response = await client.GetAsync($"/api/public/deals?category={categoria}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Equal("Score alto", "Score medio", "Score baixo");
    }

    [Fact]
    public async Task GetDeals_SortValorNaoReconhecido_CaiNoDefaultAiScore_SemErro()
    {
        var client = _factory.CreateClient();
        var categoria = $"cat-invalid-sort-{Guid.NewGuid():N}";
        await SeedPublishedProductAsync(title: "Score baixo", category: categoria, aiScore: 6);
        await SeedPublishedProductAsync(title: "Score alto", category: categoria, aiScore: 10);

        var response = await client.GetAsync(
            $"/api/public/deals?category={categoria}&sort=valor-invalido-qualquer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var titles = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("title").GetString())
            .ToList();

        titles.Should().Equal("Score alto", "Score baixo");
    }

    [Fact]
    public async Task GetDeals_FiltrosOmitidos_ComportamentoEquivalenteAoEndpointSemFiltros_CA61()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync(title: "Produto unico", aiScore: 8);

        var semFiltros = await client.GetAsync("/api/public/deals");
        var comFiltrosVazios = await client.GetAsync("/api/public/deals?category=&subcategory=");

        var bodySemFiltros = await semFiltros.Content.ReadFromJsonAsync<JsonElement>();
        var bodyComFiltrosVazios = await comFiltrosVazios.Content.ReadFromJsonAsync<JsonElement>();

        bodySemFiltros.GetProperty("items").GetArrayLength()
            .Should().Be(bodyComFiltrosVazios.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetDeals_CategoriaInexistente_Retorna200ComListaVazia_CA66()
    {
        var client = _factory.CreateClient();
        await SeedPublishedProductAsync(category: "Eletrônicos");

        var response = await client.GetAsync("/api/public/deals?category=CategoriaInexistente");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetCategories_RetornaArvoreComContagemDeProdutosPublicadosAtivos_CA67()
    {
        // Categorias/subcategorias com sufixo unico (Guid) para isolar de outros testes da
        // classe — a factory e IClassFixture (banco InMemory compartilhado por toda a suite) e
        // GetCategories nao aceita filtro por categoria para restringir a consulta.
        var suffix = Guid.NewGuid().ToString("N");
        var categoriaEletronicos = $"Eletrônicos-{suffix}";
        var categoriaCasaCozinha = $"Casa e Cozinha-{suffix}";
        var subcategoriaCelulares = $"Celulares e Smartphones-{suffix}";
        var subcategoriaAudio = $"Áudio-{suffix}";

        var client = _factory.CreateClient();
        await SeedPublishedProductAsync(
            category: categoriaEletronicos, subcategory: subcategoriaCelulares);
        await SeedPublishedProductAsync(
            category: categoriaEletronicos, subcategory: subcategoriaCelulares);
        await SeedPublishedProductAsync(category: categoriaEletronicos, subcategory: subcategoriaAudio);
        await SeedPublishedProductAsync(category: categoriaCasaCozinha, subcategory: null);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var pending = new Product(
                "Nao publicado", "desc", 10m, 20m, 50m, null,
                $"pendente-{Guid.NewGuid():N}", categoriaEletronicos, Platform.Amazon,
                externalId: Guid.NewGuid().ToString(), subcategory: subcategoriaCelulares);
            db.Products.Add(pending);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/public/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tree = await response.Content.ReadFromJsonAsync<JsonElement>();

        var eletronicos = tree.EnumerateArray()
            .Single(c => c.GetProperty("category").GetString() == categoriaEletronicos);
        eletronicos.GetProperty("count").GetInt32().Should().Be(3);

        var celulares = eletronicos.GetProperty("subcategories").EnumerateArray()
            .Single(s => s.GetProperty("subcategory").GetString() == subcategoriaCelulares);
        celulares.GetProperty("count").GetInt32().Should().Be(2);

        var audio = eletronicos.GetProperty("subcategories").EnumerateArray()
            .Single(s => s.GetProperty("subcategory").GetString() == subcategoriaAudio);
        audio.GetProperty("count").GetInt32().Should().Be(1);

        var casaECozinha = tree.EnumerateArray()
            .Single(c => c.GetProperty("category").GetString() == categoriaCasaCozinha);
        casaECozinha.GetProperty("count").GetInt32().Should().Be(1);
        casaECozinha.GetProperty("subcategories").GetArrayLength()
            .Should().Be(0, "produto sem subcategoria nao entra na lista de subcategorias, so na contagem da categoria");
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
