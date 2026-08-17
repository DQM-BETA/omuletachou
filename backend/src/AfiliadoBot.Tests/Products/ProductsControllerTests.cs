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

    private static Product NewProduct(
        string title,
        Platform platform,
        string category = "Geral",
        string? affiliateLink = "https://exemplo.com/aff",
        string? sourceUrl = null)
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
            externalId: Guid.NewGuid().ToString("N"),
            sourceUrl: sourceUrl);
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
    public async Task GetProducts_ProdutoComScore_RetornaAiScoreEAiReasonNaListagem()
    {
        // Issue #13/Sub-B (gap de contrato §2.1.1): GET /api/products (listagem) precisa expor
        // ai_score/ai_reason (mesma projecao que ja existe no detalhe) para a tabela do dashboard.
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var marker = Guid.NewGuid().ToString("N")[..8];
        var scored = NewProduct($"Produto Com Score Listagem {marker}", Platform.Amazon);
        scored.UpdateAiResult(7, "Bom desconto para a categoria.", "Aproveite agora!");

        var unscored = NewProduct($"Produto Sem Score Listagem {marker}", Platform.Amazon);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(scored);
            db.Products.Add(unscored);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/products?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        var scoredItem = items.Single(i => i.GetProperty("id").GetGuid() == scored.Id);
        scoredItem.GetProperty("ai_score").GetInt32().Should().Be(7);
        scoredItem.GetProperty("ai_reason").GetString().Should().Be("Bom desconto para a categoria.");

        var unscoredItem = items.Single(i => i.GetProperty("id").GetGuid() == unscored.Id);
        unscoredItem.GetProperty("ai_score").ValueKind.Should().Be(JsonValueKind.Null);
        unscoredItem.GetProperty("ai_reason").ValueKind.Should().Be(JsonValueKind.Null);
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
    public async Task GetProduct_ComItemFacebookNaFila_RetornaAiCaptionDoItemFacebook()
    {
        // CA12: ai_caption expõe a Caption do item de PublicationQueue mais recente da rede
        // Facebook associado ao produto — não a descrição original nem product.AiCaption.
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var product = NewProduct("Produto Com Caption Facebook", Platform.MercadoLivre);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(product);

            var telegramItem = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow, "Legenda Telegram — não deve aparecer");
            var facebookItemAntigo = new PublicationQueue(product.Id, SocialNetwork.Facebook, DateTime.UtcNow.AddMinutes(-10), "Legenda Facebook antiga");
            var facebookItemRecente = new PublicationQueue(product.Id, SocialNetwork.Facebook, DateTime.UtcNow, "Legenda Facebook mais recente");

            db.PublicationQueues.Add(telegramItem);
            db.PublicationQueues.Add(facebookItemAntigo);
            db.PublicationQueues.Add(facebookItemRecente);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/products/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ai_caption").GetString().Should().Be("Legenda Facebook mais recente");
    }

    [Fact]
    public async Task GetProduct_SemItemFacebookNaFila_RetornaAiCaptionNulo()
    {
        // CA12: produto ainda não enfileirado para Facebook — ai_caption deve ser null (distinto
        // de string vazia, que é um valor válido para itens legados com Caption='').
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var product = NewProduct("Produto Sem Caption Facebook", Platform.Amazon);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/products/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ai_caption").ValueKind.Should().Be(JsonValueKind.Null);
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

    [Fact]
    public async Task UpdateStatus_ComValorValido_AtualizaProdutoERetorna204()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var product = NewProduct("Produto Para Rejeitar", Platform.Amazon);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync($"/api/products/{product.Id}/status", new { status = "rejected" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var updated = await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
            updated.Status.Should().Be(ProductStatus.Rejected);
        }
    }

    [Fact]
    public async Task UpdateStatus_ComValorForaDoEnumPermitido_Retorna400ESemAlterar()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var product = NewProduct("Produto Status Invalido", Platform.Amazon);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync($"/api/products/{product.Id}/status", new { status = "published" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var untouched = await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
            untouched.Status.Should().Be(ProductStatus.Pending);
        }
    }

    [Fact]
    public async Task UpdateStatus_ProdutoInexistente_Retorna404()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PatchAsJsonAsync($"/api/products/{Guid.NewGuid()}/status", new { status = "rejected" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateStatus_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync($"/api/products/{Guid.NewGuid()}/status", new { status = "rejected" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Issue #182/#184 — fluxo semi-manual de link de afiliado ML.

    [Fact]
    public async Task GetProducts_FiltroStatusAwaitingAffiliateLink_RetornaApenasCorrespondentesComSourceUrl()
    {
        // Sub-B, tasks.md item 6: nenhum endpoint novo de listagem e necessario — o filtro
        // generico ja existente aceita o novo valor do enum via Enum.TryParse<ProductStatus>.
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var marker = Guid.NewGuid().ToString("N")[..8];
        var awaiting = NewProduct(
            $"Produto Aguardando Link {marker}", Platform.MercadoLivre,
            affiliateLink: null, sourceUrl: "https://www.mercadolivre.com.br/p/MLB111");
        var queued = NewProduct($"Produto Ja Enfileirado {marker}", Platform.MercadoLivre);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            awaiting.MarkAsAwaitingAffiliateLink();
            db.Products.Add(awaiting);
            db.Products.Add(queued);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/products?status=AwaitingAffiliateLink");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        items.Should().Contain(i => i.GetProperty("id").GetGuid() == awaiting.Id);
        items.Should().NotContain(i => i.GetProperty("id").GetGuid() == queued.Id);

        var awaitingItem = items.Single(i => i.GetProperty("id").GetGuid() == awaiting.Id);
        awaitingItem.GetProperty("sourceUrl").GetString().Should().Be("https://www.mercadolivre.com.br/p/MLB111");
    }

    [Fact]
    public async Task ImportAffiliateLinks_ItemValido_AtualizaProdutoParaQueuedComLink()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var product = NewProduct("Produto Import Valido", Platform.MercadoLivre, affiliateLink: null);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            product.MarkAsAwaitingAffiliateLink();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/products/affiliate-links/import", new
        {
            items = new[] { new { productId = product.Id, affiliateLink = "https://ml.link/abc" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().Should().Be(1);
        body.GetProperty("skipped").GetArrayLength().Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var updated = await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
            updated.Status.Should().Be(ProductStatus.Queued);
            updated.AffiliateLink.Should().Be("https://ml.link/abc");
        }
    }

    [Fact]
    public async Task ImportAffiliateLinks_ProductIdInexistente_EPulado()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var idInexistente = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/products/affiliate-links/import", new
        {
            items = new[] { new { productId = idInexistente, affiliateLink = "https://ml.link/abc" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().Should().Be(0);
        var skipped = body.GetProperty("skipped").EnumerateArray().ToList();
        skipped.Should().ContainSingle();
        skipped[0].GetProperty("productId").GetGuid().Should().Be(idInexistente);
        skipped[0].GetProperty("reason").GetString().Should().Contain("nao encontrado");
    }

    [Fact]
    public async Task ImportAffiliateLinks_ProdutoNaoAwaitingAffiliateLink_EPuladoENaoSobrescreveLink()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Produto ja com AffiliateLink preenchido (Status = Pending, nao AwaitingAffiliateLink).
        var product = NewProduct("Produto Ja Com Link", Platform.MercadoLivre, affiliateLink: "https://ml.link/original");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/products/affiliate-links/import", new
        {
            items = new[] { new { productId = product.Id, affiliateLink = "https://ml.link/novo-indevido" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().Should().Be(0);
        var skipped = body.GetProperty("skipped").EnumerateArray().ToList();
        skipped.Should().ContainSingle();
        skipped[0].GetProperty("reason").GetString().Should().Contain("Pending");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var untouched = await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
            untouched.AffiliateLink.Should().Be("https://ml.link/original");
        }
    }

    [Fact]
    public async Task ImportAffiliateLinks_LinkVazio_EPulado()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var product = NewProduct("Produto Link Vazio", Platform.MercadoLivre, affiliateLink: null);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            product.MarkAsAwaitingAffiliateLink();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/products/affiliate-links/import", new
        {
            items = new[] { new { productId = product.Id, affiliateLink = "   " } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().Should().Be(0);
        var skipped = body.GetProperty("skipped").EnumerateArray().ToList();
        skipped.Should().ContainSingle();
        skipped[0].GetProperty("reason").GetString().Should().Contain("Link vazio");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var untouched = await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
            untouched.Status.Should().Be(ProductStatus.AwaitingAffiliateLink);
        }
    }

    [Fact]
    public async Task ImportAffiliateLinks_LoteMisto_ImportaAlgunsEPulaOutros()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var valido = NewProduct("Produto Lote Valido", Platform.MercadoLivre, affiliateLink: null);
        var jaResolvido = NewProduct("Produto Lote Ja Resolvido", Platform.MercadoLivre, affiliateLink: "https://ml.link/existente");
        var idInexistente = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            valido.MarkAsAwaitingAffiliateLink();
            db.Products.Add(valido);
            db.Products.Add(jaResolvido);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/products/affiliate-links/import", new
        {
            items = new object[]
            {
                new { productId = valido.Id, affiliateLink = "https://ml.link/lote-valido" },
                new { productId = jaResolvido.Id, affiliateLink = "https://ml.link/tentativa-indevida" },
                new { productId = idInexistente, affiliateLink = "https://ml.link/inexistente" },
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().Should().Be(1);
        body.GetProperty("skipped").GetArrayLength().Should().Be(2);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var reloadedValido = await db.Products.AsNoTracking().FirstAsync(p => p.Id == valido.Id);
            reloadedValido.Status.Should().Be(ProductStatus.Queued);
            reloadedValido.AffiliateLink.Should().Be("https://ml.link/lote-valido");

            var reloadedJaResolvido = await db.Products.AsNoTracking().FirstAsync(p => p.Id == jaResolvido.Id);
            reloadedJaResolvido.AffiliateLink.Should().Be("https://ml.link/existente");
        }
    }

    [Fact]
    public async Task ImportAffiliateLinks_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products/affiliate-links/import", new { items = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
