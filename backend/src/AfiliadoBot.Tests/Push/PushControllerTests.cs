using System.Net;
using System.Net.Http.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Push;

/// <summary>
/// Cobre CA-E1 (subscribe sem auth), CA-E2/CA-E3 (unsubscribe idempotente) e CA-E6
/// (push funciona sem token) — Issue #11 / Sub-E.
/// </summary>
public class PushControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PushControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Subscribe_SemAuthorizationHeader_Retorna201EPersisteNoBanco()
    {
        var client = _factory.CreateClient();
        var endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/public/push/subscribe", new
        {
            endpoint,
            keys = new { p256dh = "chave-p256dh-teste", auth = "chave-auth-teste" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var saved = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        saved.Should().NotBeNull();
        saved!.P256dh.Should().Be("chave-p256dh-teste");
        saved.Auth.Should().Be("chave-auth-teste");
    }

    [Fact]
    public async Task Subscribe_SemEndpointOuKeys_Retorna400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/public/push/subscribe", new { endpoint = "", keys = (object?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unsubscribe_ComEndpointCadastrado_Retorna204ERemoveDoBanco()
    {
        var client = _factory.CreateClient();
        var endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            db.PushSubscriptions.Add(new PushSubscription(endpoint, "p256dh", "auth"));
            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/public/push/unsubscribe?endpoint={Uri.EscapeDataString(endpoint)}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        (await verifyDb.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint)).Should().BeFalse();
    }

    [Fact]
    public async Task Unsubscribe_ComEndpointNaoCadastrado_Retorna204Idempotente()
    {
        // CA-E3: 204 (nao 404), decisao documentada em especificacao-tecnica.md §6 —
        // evita vazar por enumeração se um endpoint esta cadastrado ou nao.
        var client = _factory.CreateClient();
        var endpointInexistente = "https://fcm.googleapis.com/fcm/send/nao-existe-" + Guid.NewGuid();

        var response = await client.DeleteAsync($"/api/public/push/unsubscribe?endpoint={Uri.EscapeDataString(endpointInexistente)}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unsubscribe_SemEndpoint_Retorna400()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/public/push/unsubscribe");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
