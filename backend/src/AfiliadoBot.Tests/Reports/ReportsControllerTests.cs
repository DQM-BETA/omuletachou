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

    // Issue #228/T-02: builder usado pelos testes de ProductsSummary — categoria/subcategoria/
    // plataforma controladas para exercitar filtro e breakdown.
    private static Product BuildProduct(string category, string? subcategory, Platform platform)
    {
        return new Product(
            title: "Produto Teste " + Guid.NewGuid(),
            description: "Descricao",
            salePrice: 99.90m,
            originalPrice: 149.90m,
            discountPct: 33m,
            affiliateLink: "https://example.com/aff",
            slug: "produto-teste-" + Guid.NewGuid(),
            category: category,
            platform: platform,
            imageUrl: "https://example.com/img.png",
            externalId: "ext-" + Guid.NewGuid(),
            sourceUrl: "https://example.com/src",
            subcategory: subcategory);
    }

    // CreatedAt tem setter privado (definido em DateTime.UtcNow no construtor) — precisamos
    // forcar datas especificas para exercitar a janela [from, toExclusive) de collectedFrom/
    // collectedTo (CA 2.5), mesmo padrao de reflection ja usado por SetPublishedAt acima.
    private static void SetCreatedAt(Product product, DateTime createdAt)
    {
        var prop = typeof(Product).GetProperty(nameof(Product.CreatedAt),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        prop.SetValue(product, createdAt);
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
            var q1 = new PublicationQueue(product.Id, SocialNetwork.Telegram, today, "Legenda de teste");
            q1.RegisterAttempt(success: true);
            SetPublishedAt(q1, today.AddHours(10));

            var q2 = new PublicationQueue(product.Id, SocialNetwork.Instagram, today.AddDays(-2), "Legenda de teste");
            q2.RegisterAttempt(success: true);
            SetPublishedAt(q2, today.AddDays(-2).AddHours(9));

            var q3 = new PublicationQueue(product.Id, SocialNetwork.Telegram, today.AddDays(-2), "Legenda de teste");
            q3.RegisterAttempt(success: true);
            SetPublishedAt(q3, today.AddDays(-2).AddHours(15));

            // Fora da janela (8 dias atras) — nao deve entrar no total
            var qOld = new PublicationQueue(product.Id, SocialNetwork.Telegram, today.AddDays(-8), "Legenda de teste");
            qOld.RegisterAttempt(success: true);
            SetPublishedAt(qOld, today.AddDays(-8));

            // Falha — nao deve entrar (Status != Published)
            var qFailed = new PublicationQueue(product.Id, SocialNetwork.Youtube, today, "Legenda de teste");
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
            var qToday = new PublicationQueue(product.Id, SocialNetwork.Telegram, today, "Legenda de teste");
            qToday.RegisterAttempt(success: true);
            SetPublishedAt(qToday, today.AddHours(5));

            // Publicado dentro do mes, mas antes do inicio da semana ISO corrente (ou no inicio do
            // mes, se a semana corrente comecar no dia 1) — deve contar em month, mas nao em week.
            var beforeWeekButInMonth = weekStart.AddDays(-1) >= monthStart ? weekStart.AddDays(-1) : monthStart;
            var qMonthOnly = new PublicationQueue(product.Id, SocialNetwork.Instagram, beforeWeekButInMonth, "Legenda de teste");
            qMonthOnly.RegisterAttempt(success: true);
            SetPublishedAt(qMonthOnly, beforeWeekButInMonth.AddHours(3));

            // Publicado ha muito tempo (fora do mes corrente) — nao deve contar em nenhum total.
            var qOld = new PublicationQueue(product.Id, SocialNetwork.Telegram, monthStart.AddMonths(-2), "Legenda de teste");
            qOld.RegisterAttempt(success: true);
            SetPublishedAt(qOld, monthStart.AddMonths(-2));

            // Falha — nao deve contar em nenhum total (Status != Published).
            var qFailed = new PublicationQueue(product.Id, SocialNetwork.Youtube, today, "Legenda de teste");
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

    // ---- Issue #228/T-02: GET /api/reports/products/summary --------------------------------
    // Cada teste usa uma categoria unica (Guid) para isolar seus produtos dos demais testes da
    // classe, que compartilham o mesmo banco InMemory via CustomWebApplicationFactory
    // (IClassFixture) — mesma tecnica das secoes acima usarem delta/baseline.

    [Fact]
    public async Task ProductsSummary_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/products/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProductsSummary_ComStatusPublished_RetornaTotalEOs4BreakdownsCorretos()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var category = "Eletronicos-" + Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            var p1 = BuildProduct(category, "Celulares", Platform.MercadoLivre);
            p1.MarkAsPublished();
            var p2 = BuildProduct(category, "Celulares", Platform.MercadoLivre);
            p2.MarkAsPublished();
            var p3 = BuildProduct(category, "Notebooks", Platform.Amazon);
            p3.MarkAsPublished();

            // Mesma categoria, mas Pending — nao deve contar com status=Published explicito.
            var pPending = BuildProduct(category, "Celulares", Platform.MercadoLivre);

            db.Products.AddRange(p1, p2, p3, pPending);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            $"/api/reports/products/summary?status=Published&category={Uri.EscapeDataString(category)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("total").GetInt32().Should().Be(3);

        var byPlatform = body.GetProperty("byPlatform").EnumerateArray()
            .ToDictionary(e => e.GetProperty("platform").GetString()!, e => e.GetProperty("count").GetInt32());
        byPlatform.GetValueOrDefault("MercadoLivre").Should().Be(2);
        byPlatform.GetValueOrDefault("Amazon").Should().Be(1);

        var byCategory = body.GetProperty("byCategory").EnumerateArray()
            .ToDictionary(e => e.GetProperty("category").GetString()!, e => e.GetProperty("count").GetInt32());
        byCategory.GetValueOrDefault(category).Should().Be(3);

        var byStatus = body.GetProperty("byStatus").EnumerateArray()
            .ToDictionary(e => e.GetProperty("status").GetString()!, e => e.GetProperty("count").GetInt32());
        byStatus.GetValueOrDefault("Published").Should().Be(3);
        byStatus.ContainsKey("Pending").Should().BeFalse();

        var bySubcategory = body.GetProperty("bySubcategory").EnumerateArray()
            .ToDictionary(e => e.GetProperty("subcategory").GetString()!, e => e.GetProperty("count").GetInt32());
        bySubcategory.GetValueOrDefault("Celulares").Should().Be(2);
        bySubcategory.GetValueOrDefault("Notebooks").Should().Be(1);
    }

    [Fact]
    public async Task ProductsSummary_ComStatusPendingExplicito_NaoRestringeAPublished()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var category = "Moda-" + Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            var pending1 = BuildProduct(category, "Camisetas", Platform.Shopee);
            var pending2 = BuildProduct(category, "Camisetas", Platform.Shopee);

            var published = BuildProduct(category, "Camisetas", Platform.Shopee);
            published.MarkAsPublished();

            db.Products.AddRange(pending1, pending2, published);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            $"/api/reports/products/summary?status=Pending&category={Uri.EscapeDataString(category)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("total").GetInt32().Should().Be(2);
        var byStatus = body.GetProperty("byStatus").EnumerateArray()
            .ToDictionary(e => e.GetProperty("status").GetString()!, e => e.GetProperty("count").GetInt32());
        byStatus.GetValueOrDefault("Pending").Should().Be(2);
        byStatus.ContainsKey("Published").Should().BeFalse();
    }

    [Fact]
    public async Task ProductsSummary_ComFiltrosCombinados_RetornaIntersecaoAND()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var category = "Combinado-" + Guid.NewGuid();
        var from = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            // Casa TODOS os filtros: platform=MercadoLivre, category, dentro da janela.
            var match = BuildProduct(category, "Sub", Platform.MercadoLivre);
            match.MarkAsPublished();
            SetCreatedAt(match, from.AddDays(2));

            // Plataforma diferente — nao deve casar.
            var wrongPlatform = BuildProduct(category, "Sub", Platform.Amazon);
            wrongPlatform.MarkAsPublished();
            SetCreatedAt(wrongPlatform, from.AddDays(2));

            // Mesma categoria/plataforma, mas fora da janela de datas — nao deve casar.
            var outOfRange = BuildProduct(category, "Sub", Platform.MercadoLivre);
            outOfRange.MarkAsPublished();
            SetCreatedAt(outOfRange, from.AddDays(-5));

            db.Products.AddRange(match, wrongPlatform, outOfRange);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/reports/products/summary" +
            $"?platform=MercadoLivre&category={Uri.EscapeDataString(category)}" +
            $"&collectedFrom={from:yyyy-MM-dd}&collectedTo={to:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("total").GetInt32().Should().Be(1);
        var byPlatform = body.GetProperty("byPlatform").EnumerateArray()
            .ToDictionary(e => e.GetProperty("platform").GetString()!, e => e.GetProperty("count").GetInt32());
        byPlatform.Should().ContainKey("MercadoLivre").WhoseValue.Should().Be(1);
        byPlatform.ContainsKey("Amazon").Should().BeFalse();
    }

    [Fact]
    public async Task ProductsSummary_SemMatch_Retorna200ComTotalZeroEBreakdownsVazios()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var category = "InexistenteNoBanco-" + Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/reports/products/summary?category={Uri.EscapeDataString(category)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("total").GetInt32().Should().Be(0);
        body.GetProperty("byPlatform").EnumerateArray().Should().BeEmpty();
        body.GetProperty("byCategory").EnumerateArray().Should().BeEmpty();
        body.GetProperty("byStatus").EnumerateArray().Should().BeEmpty();
        body.GetProperty("bySubcategory").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task ProductsSummary_ComPlatformInvalida_NaoRetorna400EResultaSemMatch()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var category = "PlataformaInvalida-" + Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var product = BuildProduct(category, null, Platform.Amazon);
            product.MarkAsPublished();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            $"/api/reports/products/summary?category={Uri.EscapeDataString(category)}&platform=NaoExiste");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ProductsSummary_ComCollectedFromECollectedTo_JanelaInclusivaNosLimites()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var category = "JanelaData-" + Guid.NewGuid();
        var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            // Exatamente no limite inicial (inicio do dia) — deve entrar.
            var onFrom = BuildProduct(category, null, Platform.Amazon);
            SetCreatedAt(onFrom, from);

            // Exatamente no limite final (qualquer hora dentro do dia final) — deve entrar.
            var onTo = BuildProduct(category, null, Platform.Amazon);
            SetCreatedAt(onTo, to.AddHours(23).AddMinutes(59));

            // Um dia antes do limite inicial — nao deve entrar.
            var beforeFrom = BuildProduct(category, null, Platform.Amazon);
            SetCreatedAt(beforeFrom, from.AddDays(-1));

            // Um dia depois do limite final (fora da janela) — nao deve entrar.
            var afterTo = BuildProduct(category, null, Platform.Amazon);
            SetCreatedAt(afterTo, to.AddDays(2));

            db.Products.AddRange(onFrom, onTo, beforeFrom, afterTo);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/reports/products/summary" +
            $"?category={Uri.EscapeDataString(category)}&collectedFrom={from:yyyy-MM-dd}&collectedTo={to:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(2);
    }
}
