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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Baseline capturado ANTES de inserir os dados deste teste: a factory compartilha o
        // banco InMemory entre todos os testes desta classe (CustomWebApplicationFactory), entao
        // outros testes (ex.: Totals_*) podem ja ter publicado itens dentro da mesma janela de 7
        // dias. Asserts em delta (depois - antes) tornam o teste independente da ordem/estado de
        // outros testes da classe.
        var baselineResponse = await client.GetAsync("/api/reports/summary");
        var baselineBody = await baselineResponse.Content.ReadFromJsonAsync<JsonElement>();
        var baselineTotal = baselineBody.GetProperty("totalPublished").GetInt32();
        var baselineByNetwork = baselineBody.GetProperty("byNetwork").EnumerateArray()
            .ToDictionary(e => e.GetProperty("network").GetString()!, e => e.GetProperty("count").GetInt32());

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

        var response = await client.GetAsync("/api/reports/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        (body.GetProperty("totalPublished").GetInt32() - baselineTotal).Should().Be(3);

        var byNetwork = body.GetProperty("byNetwork").EnumerateArray()
            .ToDictionary(e => e.GetProperty("network").GetString()!, e => e.GetProperty("count").GetInt32());
        (byNetwork.GetValueOrDefault("Telegram") - baselineByNetwork.GetValueOrDefault("Telegram")).Should().Be(2);
        (byNetwork.GetValueOrDefault("Instagram") - baselineByNetwork.GetValueOrDefault("Instagram")).Should().Be(1);

        var byDay = body.GetProperty("byDay").EnumerateArray().ToList();
        var baselineByDaySum = baselineBody.GetProperty("byDay").EnumerateArray()
            .Sum(d => d.GetProperty("count").GetInt32());
        (byDay.Sum(d => d.GetProperty("count").GetInt32()) - baselineByDaySum).Should().Be(3);
    }

    [Fact]
    public async Task Totals_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/totals");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Totals_ComTokenValido_RetornaContagensHojeSemanaEMes()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);

        var today = DateTime.UtcNow.Date;
        var diffToMonday = ((int)today.DayOfWeek + 6) % 7;
        var weekStart = today.AddDays(-diffToMonday);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            var product = BuildProduct();
            db.Products.Add(product);

            // Publicado hoje — deve contar em today/week/month.
            var qToday = new PublicationQueue(product.Id, SocialNetwork.Telegram, today);
            qToday.RegisterAttempt(success: true);
            SetPublishedAt(qToday, today.AddHours(5));

            // Publicado dentro do mes, mas antes do inicio da semana ISO corrente (ou no inicio do
            // mes, se a semana corrente comecar no dia 1) — deve contar em month, mas nao em week.
            var beforeWeekButInMonth = weekStart.AddDays(-1) >= monthStart ? weekStart.AddDays(-1) : monthStart;
            var qMonthOnly = new PublicationQueue(product.Id, SocialNetwork.Instagram, beforeWeekButInMonth);
            qMonthOnly.RegisterAttempt(success: true);
            SetPublishedAt(qMonthOnly, beforeWeekButInMonth.AddHours(3));

            // Publicado ha muito tempo (fora do mes corrente) — nao deve contar em nenhum total.
            var qOld = new PublicationQueue(product.Id, SocialNetwork.Telegram, monthStart.AddMonths(-2));
            qOld.RegisterAttempt(success: true);
            SetPublishedAt(qOld, monthStart.AddMonths(-2));

            // Falha — nao deve contar em nenhum total (Status != Published).
            var qFailed = new PublicationQueue(product.Id, SocialNetwork.Youtube, today);
            qFailed.RegisterAttempt(success: false, errorMessage: "erro simulado");

            db.PublicationQueues.AddRange(qToday, qMonthOnly, qOld, qFailed);
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/reports/totals");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("today").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("week").GetInt32().Should().BeGreaterThanOrEqualTo(body.GetProperty("today").GetInt32());
        body.GetProperty("month").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }
}
