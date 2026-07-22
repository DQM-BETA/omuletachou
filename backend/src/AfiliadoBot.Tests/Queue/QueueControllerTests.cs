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

namespace AfiliadoBot.Tests.Queue;

/// <summary>
/// Cobre CA-B7 e CA-B11 (Issue #11 / Sub-B, #82) para QueueController: paginacao
/// default/customizada/truncamento, filtros status/network, 401 sem token.
/// </summary>
public class QueueControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SeededEmail = "operador-fila@omuletachou.com.br";
    private const string SeededPassword = "SenhaForte#2026";

    private readonly CustomWebApplicationFactory _factory;

    public QueueControllerTests(CustomWebApplicationFactory factory)
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

    private static Product NewProduct(string title)
    {
        return new Product(
            title: title,
            description: "Descricao de " + title,
            salePrice: 49.90m,
            originalPrice: 99.90m,
            discountPct: 50m,
            affiliateLink: "https://exemplo.com/aff",
            slug: title.ToLowerInvariant().Replace(' ', '-') + "-" + Guid.NewGuid().ToString("N")[..8],
            category: "Geral",
            platform: Platform.Amazon,
            externalId: Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task GetQueue_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/queue");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetQueue_SemParametros_Retorna200ComPaginacaoDefault()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var product = NewProduct("Produto Fila Default");
            db.Products.Add(product);
            db.PublicationQueues.Add(new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/queue");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetQueue_ComPageSizeAcimaDoMaximo_TruncaPara100()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/queue?pageSize=500");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pageSize").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task GetQueue_FiltroStatusENetwork_RetornaApenasCorrespondentes()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid matchingId;
        Guid wrongNetworkId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            var productA = NewProduct("Produto Fila Match");
            var productB = NewProduct("Produto Fila Wrong Network");
            db.Products.Add(productA);
            db.Products.Add(productB);

            var matching = new PublicationQueue(productA.Id, SocialNetwork.Youtube, DateTime.UtcNow);
            var wrongNetwork = new PublicationQueue(productB.Id, SocialNetwork.Instagram, DateTime.UtcNow);
            matchingId = matching.Id;
            wrongNetworkId = wrongNetwork.Id;

            db.PublicationQueues.Add(matching);
            db.PublicationQueues.Add(wrongNetwork);
            await db.SaveChangesAsync();
        }

        // Ambos nascem com Status = Scheduled (construtor de PublicationQueue) — filtro exercita
        // "scheduled" (lowercase) + network=youtube.
        var response = await client.GetAsync("/api/queue?status=scheduled&network=youtube");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        items.Should().Contain(i => i.GetProperty("id").GetGuid() == matchingId);
        items.Should().NotContain(i => i.GetProperty("id").GetGuid() == wrongNetworkId);
    }

    [Fact]
    public async Task GetManualQueue_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/queue/manual");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetManualQueue_RetornaApenasItensManualPending()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid manualId;
        Guid scheduledId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            var productA = NewProduct("Produto Fila Manual");
            var productB = NewProduct("Produto Fila Agendado");
            db.Products.Add(productA);
            db.Products.Add(productB);

            var manual = new PublicationQueue(productA.Id, SocialNetwork.Facebook, DateTime.UtcNow);
            manual.MarkAsManualPending();
            var scheduled = new PublicationQueue(productB.Id, SocialNetwork.Telegram, DateTime.UtcNow);
            manualId = manual.Id;
            scheduledId = scheduled.Id;

            db.PublicationQueues.Add(manual);
            db.PublicationQueues.Add(scheduled);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/queue/manual");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        items.Should().Contain(i => i.GetProperty("id").GetGuid() == manualId);
        items.Should().NotContain(i => i.GetProperty("id").GetGuid() == scheduledId);
        items.Should().OnlyContain(i => i.GetProperty("status").GetString() == "ManualPending");
    }

    [Fact]
    public async Task RetryQueueItem_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/queue/{Guid.NewGuid()}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RetryQueueItem_ItemComFalha_AtualizaParaScheduledERetorna204()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid itemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var product = NewProduct("Produto Fila Falha");
            db.Products.Add(product);

            var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(-1));
            item.RegisterAttempt(success: false, errorMessage: "Timeout ao publicar.");
            itemId = item.Id;

            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();
        }

        var before = DateTime.UtcNow;
        var response = await client.PostAsync($"/api/queue/{itemId}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var updated = await db.PublicationQueues.AsNoTracking().FirstAsync(q => q.Id == itemId);
            updated.Status.Should().Be(PublicationStatus.Scheduled);
            updated.RetryCount.Should().Be(0);
            updated.ErrorMessage.Should().BeNull();
            updated.ScheduledAt.Should().BeOnOrAfter(before);
        }
    }

    [Fact]
    public async Task RetryQueueItem_ItemQueNaoEstaEmFalha_Retorna409ESemAlterar()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid itemId;
        DateTime originalScheduledAt;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var product = NewProduct("Produto Fila Nao Falha");
            db.Products.Add(product);

            var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow.AddHours(1));
            itemId = item.Id;
            originalScheduledAt = item.ScheduledAt;

            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync($"/api/queue/{itemId}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var untouched = await db.PublicationQueues.AsNoTracking().FirstAsync(q => q.Id == itemId);
            untouched.Status.Should().Be(PublicationStatus.Scheduled);
            untouched.ScheduledAt.Should().BeCloseTo(originalScheduledAt, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task RetryQueueItem_ItemInexistente_Retorna404()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/queue/{Guid.NewGuid()}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateStatus_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync($"/api/queue/{Guid.NewGuid()}/status", new { status = "Published" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateStatus_ItemManualPending_AtualizaParaPublishedERetorna204()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid itemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var product = NewProduct("Produto Fila Facebook Manual");
            db.Products.Add(product);

            var item = new PublicationQueue(product.Id, SocialNetwork.Facebook, DateTime.UtcNow);
            item.MarkAsManualPending();
            itemId = item.Id;

            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();
        }

        var before = DateTime.UtcNow;
        var response = await client.PatchAsJsonAsync($"/api/queue/{itemId}/status", new { status = "Published" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var updated = await db.PublicationQueues.AsNoTracking().FirstAsync(q => q.Id == itemId);
            updated.Status.Should().Be(PublicationStatus.Published);
            updated.PublishedAt.Should().NotBeNull();
            updated.PublishedAt.Should().BeOnOrAfter(before);
        }
    }

    [Fact]
    public async Task UpdateStatus_TransicaoInvalida_Retorna400ESemAlterar()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid itemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var product = NewProduct("Produto Fila Transicao Invalida");
            db.Products.Add(product);

            var item = new PublicationQueue(product.Id, SocialNetwork.Facebook, DateTime.UtcNow);
            item.MarkAsManualPending();
            itemId = item.Id;

            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync($"/api/queue/{itemId}/status", new { status = "Failed" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var untouched = await db.PublicationQueues.AsNoTracking().FirstAsync(q => q.Id == itemId);
            untouched.Status.Should().Be(PublicationStatus.ManualPending);
        }
    }

    [Fact]
    public async Task UpdateStatus_ItemQueNaoEstaManualPending_Retorna409ESemAlterar()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid itemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var product = NewProduct("Produto Fila Nao Manual Pending");
            db.Products.Add(product);

            var item = new PublicationQueue(product.Id, SocialNetwork.Telegram, DateTime.UtcNow);
            itemId = item.Id;

            db.PublicationQueues.Add(item);
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync($"/api/queue/{itemId}/status", new { status = "Published" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var untouched = await db.PublicationQueues.AsNoTracking().FirstAsync(q => q.Id == itemId);
            untouched.Status.Should().Be(PublicationStatus.Scheduled);
            untouched.PublishedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task UpdateStatus_ItemInexistente_Retorna404()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PatchAsJsonAsync($"/api/queue/{Guid.NewGuid()}/status", new { status = "Published" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
