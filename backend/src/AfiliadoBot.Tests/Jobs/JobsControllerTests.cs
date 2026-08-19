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

namespace AfiliadoBot.Tests.Jobs;

/// <summary>
/// Testes de integracao (WebApplicationFactory + EF InMemory) de JobsController (Issue #227):
/// POST .../trigger cria um JobRun para o JobName correspondente (CA 1.1/5.1), credenciais
/// ausentes continuam retornando 400 e registram JobRun Failed (CA 2.1 — regressao +
/// tracking), e GET /api/jobs/last-executions agrega a ultima execucao dos 6 jobs (CA 2.2, 3.1,
/// 4.3, 5.2).
/// </summary>
public class JobsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SeededEmail = "jobs-operador@omuletachou.com.br";
    private const string SeededPassword = "SenhaForte#2026";

    private readonly CustomWebApplicationFactory _factory;

    public JobsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> AuthenticateAsync(CustomWebApplicationFactory factory, HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
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

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var token = await AuthenticateAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // --- Auth (regressao, CA-C10) ---

    [Fact]
    public async Task GetLastExecutions_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/jobs/last-executions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- CA 1.1/5.1: POST .../trigger cria JobRun ---

    [Fact]
    public async Task TriggerCollector_CriaJobRun_ParaJobNameCollector()
    {
        var client = await CreateAuthenticatedClientAsync(_factory);

        var response = await client.PostAsync("/api/jobs/collector/trigger", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var run = await db.JobRuns
            .Where(r => r.JobName == JobName.Collector)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        run.Should().NotBeNull();
        // Nuance §2.2 do design.md: CollectorJob isola falha por plataforma internamente — o card
        // "Collector (geral)" so aparece Failed se ExecuteAsync lancar excecao nao tratada, nao
        // quando plataformas individuais falham (sem credenciais configuradas nos testes).
        run!.Status.Should().Be(JobRunStatus.Success);
    }

    [Fact]
    public async Task TriggerProcessor_CriaJobRun_ParaJobNameProcessor()
    {
        var client = await CreateAuthenticatedClientAsync(_factory);

        var response = await client.PostAsync("/api/jobs/processor/trigger", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var run = await db.JobRuns
            .Where(r => r.JobName == JobName.Processor)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        run.Should().NotBeNull();
        run!.Status.Should().Be(JobRunStatus.Success);
    }

    [Fact]
    public async Task TriggerPublisher_CriaJobRun_ParaJobNamePublisher()
    {
        var client = await CreateAuthenticatedClientAsync(_factory);

        var response = await client.PostAsync("/api/jobs/publisher/trigger", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var run = await db.JobRuns
            .Where(r => r.JobName == JobName.Publisher)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        run.Should().NotBeNull();
        run!.Status.Should().Be(JobRunStatus.Success);
    }

    // --- CA 2.1: credenciais ausentes -> 400 + JobRun Failed (regressao + tracking) ---

    [Theory]
    [InlineData("/api/jobs/collector/amazon/trigger", JobName.CollectorAmazon, "amazon")]
    [InlineData("/api/jobs/collector/mercadolivre/trigger", JobName.CollectorMercadoLivre, "mercadolivre")]
    [InlineData("/api/jobs/collector/shopee/trigger", JobName.CollectorShopee, "shopee")]
    public async Task TriggerCollectorIndividual_SemCredenciais_Retorna400EJobRunFailed(
        string path, JobName jobName, string platformSlug)
    {
        var client = await CreateAuthenticatedClientAsync(_factory);

        var response = await client.PostAsync(path, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Contain(platformSlug);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var run = await db.JobRuns
            .Where(r => r.JobName == jobName)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        run.Should().NotBeNull();
        run!.Status.Should().Be(JobRunStatus.Failed);
        run.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    // --- GET /api/jobs/last-executions (CA 2.2, 3.1, 4.3, 5.2) — factory isolada por teste para
    // nao sofrer interferencia dos JobRuns criados pelos testes de trigger acima (mesma classe
    // compartilha 1 instancia de CustomWebApplicationFactory/DB via IClassFixture).

    [Fact]
    public async Task GetLastExecutions_BancoVazio_RetornaAsSeisEntradasComStatusNull()
    {
        // CA 3.1: job sem nenhum JobRun no banco retorna status/startedAt/finishedAt = null —
        // sempre as 6 entradas, mesmo sem nenhum historico.
        using var isolatedFactory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(isolatedFactory);

        var response = await client.GetAsync("/api/jobs/last-executions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();

        items.Should().HaveCount(6);
        items.Should().OnlyContain(i =>
            i.GetProperty("status").ValueKind == JsonValueKind.Null &&
            i.GetProperty("startedAt").ValueKind == JsonValueKind.Null &&
            i.GetProperty("finishedAt").ValueKind == JsonValueKind.Null &&
            i.GetProperty("errorMessage").ValueKind == JsonValueKind.Null);

        var slugs = items.Select(i => i.GetProperty("jobName").GetString()).ToList();
        slugs.Should().BeEquivalentTo(new[]
        {
            "collector", "collector-amazon", "collector-mercadolivre",
            "collector-shopee", "processor", "publisher",
        });
    }

    [Fact]
    public async Task GetLastExecutions_MultiplasExecucoes_RetornaApenasAMaisRecente()
    {
        // CA 4.3: job com multiplas execucoes retorna so a de StartedAt mais recente; as
        // execucoes antigas continuam no banco (nao deletadas), so nao aparecem no endpoint.
        using var isolatedFactory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(isolatedFactory);

        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

            // Older marcado como Failed e newer como Success: se o endpoint (erroneamente)
            // retornasse o mais antigo, o teste pegaria "failed" em vez de "success".
            var older = JobRun.Start(JobName.Publisher);
            older.MarkAsFailed("erro antigo, nao deve aparecer no endpoint");
            SetStartedAt(older, DateTime.UtcNow.AddDays(-2));
            db.JobRuns.Add(older);

            var newer = JobRun.Start(JobName.Publisher);
            newer.MarkAsSuccess();
            SetStartedAt(newer, DateTime.UtcNow.AddHours(-1));
            db.JobRuns.Add(newer);

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/jobs/last-executions");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var publisherEntry = body.EnumerateArray().Single(i => i.GetProperty("jobName").GetString() == "publisher");

        publisherEntry.GetProperty("status").GetString().Should().Be("success");

        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            (await db.JobRuns.CountAsync(r => r.JobName == JobName.Publisher)).Should().Be(2);
        }
    }

    [Fact]
    public async Task GetLastExecutions_UltimaExecucaoFalhou_RetornaStatusFailedComErrorMessage()
    {
        // CA 2.2: ultima execucao com falha retorna status "failed" com
        // startedAt/finishedAt/errorMessage preenchidos — nunca "success" para uma execucao
        // que falhou.
        using var isolatedFactory = new CustomWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(isolatedFactory);

        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
            var failedRun = JobRun.Start(JobName.CollectorAmazon);
            failedRun.MarkAsFailed("Credenciais não configuradas para amazon.");
            db.JobRuns.Add(failedRun);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/jobs/last-executions");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entry = body.EnumerateArray().Single(i => i.GetProperty("jobName").GetString() == "collector-amazon");

        entry.GetProperty("status").GetString().Should().Be("failed");
        entry.GetProperty("startedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        entry.GetProperty("finishedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        entry.GetProperty("errorMessage").GetString().Should().Be("Credenciais não configuradas para amazon.");
    }

    // Forca StartedAt via reflection (setter privado — Start() sempre usa DateTime.UtcNow) para
    // simular execucoes em momentos diferentes de forma deterministica no teste.
    private static void SetStartedAt(JobRun run, DateTime startedAt)
    {
        var prop = typeof(JobRun).GetProperty(nameof(JobRun.StartedAt),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        prop.SetValue(run, startedAt);
    }
}
