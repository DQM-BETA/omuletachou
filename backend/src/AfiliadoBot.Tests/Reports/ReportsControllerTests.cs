using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Reports;

/// <summary>
/// Cobre CA-E5 (resumo dos ultimos 7 dias agrupado por rede/dia) e CA-E6 (protegido:
/// 401 sem token) — Issue #11 / Sub-E.
/// </summary>
public class ReportsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SeededEmail = "reports-operador@omuletachou.com.br";
    private const string SeededPassword = "SenhaForte#2026";

    private readonly CustomWebApplicationFactory _factory;

    public ReportsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> LoginAndGetTokenAsync(HttpClient client)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            if (!await db.Users.AnyAsync(u => u.Email == SeededEmail))
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(SeededPassword, workFactor: 12);
                db.Users.Add(new User(SeededEmail, hash));
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = SeededEmail, password = SeededPassword });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return loginBody.GetProperty("token").GetString()!;
    }

    // PublishedAt tem setter privado (materializado via RegisterAttempt = DateTime.UtcNow) —
    // para testar a janela/agrupamento por dia precisamos forcar datas especificas no passado.
    private static void SetPublishedAt(PublicationQueue queue, DateTime publishedAt)
    {
        var prop = typeof(PublicationQueue).GetProperty(nameof(PublicationQueue.PublishedAt),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        prop.SetValue(queue, publishedAt);
    }

    private static Product BuildProduct()
    {
        return new Product(
            title: "Produto Teste",
            description: "Descricao",
            salePrice: 99.90m,
            originalPrice: 149.90m,
            discountPct: 33m,
            affiliateLink: "https://example.com/aff",
            slug: "produto-teste-" + Guid.NewGuid(),
            category: "eletronicos",
            platform: Platform.Amazon,
            imageUrl: "https://example.com/img.png",
            externalId: "ext-" + Guid.NewGuid(),
            sourceUrl: "https://example.com/src");
    }

    [Fact]
    public async Task Summary_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Summary_ComTokenValido_RetornaAgregadoPorRedeEporDiaDosUltimos7Dias()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            var product = BuildProduct();
            db.Products.Add(product);

            var today = DateTime.UtcNow.Date;

            // Dentro da janela de 7 dias, redes distintas
            var q1 = new PublicationQueue(product.Id, SocialNetwork.Telegram, today);
            q1.RegisterAttempt(success: true);
            SetPublishedAt(q1, today.AddHours(10));

            var q2 = new PublicationQueue(product.Id, SocialNetwork.Instagram, today.AddDays(-2));
            q2.RegisterAttempt(success: true);
            SetPublishedAt(q2, today.AddDays(-2).AddHours(9));

            var q3 = new PublicationQueue(product.Id, SocialNetwork.Telegram, today.AddDays(-2));
            q3.RegisterAttempt(success: true);
            SetPublishedAt(q3, today.AddDays(-2).AddHours(15));

            // Fora da janela (8 dias atras) — nao deve entrar no total
            var qOld = new PublicationQueue(product.Id, SocialNetwork.Telegram, today.AddDays(-8));
            qOld.RegisterAttempt(success: true);
            SetPublishedAt(qOld, today.AddDays(-8));

            // Falha — nao deve entrar (Status != Published)
            var qFailed = new PublicationQueue(product.Id, SocialNetwork.Youtube, today);
            qFailed.RegisterAttempt(success: false, errorMessage: "erro simulado");

            db.PublicationQueues.AddRange(q1, q2, q3, qOld, qFailed);
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/reports/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalPublished").GetInt32().Should().Be(3);

        var byNetwork = body.GetProperty("byNetwork").EnumerateArray()
            .ToDictionary(e => e.GetProperty("network").GetString()!, e => e.GetProperty("count").GetInt32());
        byNetwork["Telegram"].Should().Be(2);
        byNetwork["Instagram"].Should().Be(1);
        byNetwork.Should().NotContainKey("Youtube");

        var byDay = body.GetProperty("byDay").EnumerateArray().ToList();
        byDay.Sum(d => d.GetProperty("count").GetInt32()).Should().Be(3);
    }
}
