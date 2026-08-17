using System.Text.Json;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AfiliadoBot.Tests.Services;

/// <summary>
/// Teste de integracao real (Issue #167 — Sub-B/#169, design.md §3.5): sobe um Postgres real
/// via Testcontainers e roda as migrations reais do projeto (inclui os seeds de
/// "claude.monthly_usage" etc. — migration AddSubcategoryAndCategorizationBudget da Sub-A/#168).
/// Cobre exatamente o que o teste unitario com InMemory NAO pode cobrir: a atomicidade do
/// UPDATE...CASE (ExecuteSqlInterpolatedAsync) sob concorrencia real — o cenario descrito em
/// design.md §3.5 (disparo manual do ProcessorJob sobrepondo o cron, ou N produtos processados
/// "em paralelo" dentro do mesmo ciclo).
/// </summary>
public class ClaudeBudgetServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16.14-alpine")
        .WithDatabase("afiliadoBot_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private AfiliadoBotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    [Fact]
    public async Task RecordUsageAsync_AtomicoSobConcorrencia_SemLostUpdate()
    {
        // 20 chamadas concorrentes de RecordUsageAsync (cada uma abrindo seu proprio
        // DbContext/conexao, simulando execucoes concorrentes reais do ProcessorJob) — se o
        // UPDATE...CASE nao fosse atomico, o classico "lost update" faria a soma final ficar
        // menor que 20x o incremento individual.
        const int concurrentCalls = 20;
        const int inputTokens = 1_000_000; // 1 USD (preco default seedado = 1 USD/MTok)
        const int outputTokens = 0;

        var tasks = Enumerable.Range(0, concurrentCalls).Select(async _ =>
        {
            using var db = CreateContext();
            var service = new ClaudeBudgetService(db);
            await service.RecordUsageAsync(inputTokens, outputTokens);
        });

        await Task.WhenAll(tasks);

        using var verifyDb = CreateContext();
        var usageJson = await verifyDb.AppSettings
            .Where(s => s.Key == "claude.monthly_usage")
            .Select(s => s.Value)
            .FirstAsync();

        using var doc = JsonDocument.Parse(usageJson);
        var spend = doc.RootElement.GetProperty("spend_brl").GetDecimal();

        // Cada chamada custa 1 USD * 5.5 (taxa default seedada) = 5.5 BRL. 20 chamadas -> 110.
        spend.Should().Be(110m);
    }

    [Fact]
    public async Task RecordUsageAsync_ReiniciaContador_QuandoMesSalvoDivergeDoAtual_ContraPostgresReal()
    {
        using var seedDb = CreateContext();
        // Sobrescreve o seed da migration (mes vazio) com um mes antigo com gasto alto, para
        // validar que a escrita reinicia (nao soma) quando o mes diverge — CA 4.5.
        var setting = await seedDb.AppSettings.FirstAsync(s => s.Key == "claude.monthly_usage");
        setting.UpdateValue("{\"month\":\"2020-01\",\"spend_brl\":9999}");
        await seedDb.SaveChangesAsync();

        using var db = CreateContext();
        var service = new ClaudeBudgetService(db);
        await service.RecordUsageAsync(inputTokens: 1_000_000, outputTokens: 0); // 5.5 BRL

        using var verifyDb = CreateContext();
        var usageJson = await verifyDb.AppSettings
            .Where(s => s.Key == "claude.monthly_usage")
            .Select(s => s.Value)
            .FirstAsync();

        using var doc = JsonDocument.Parse(usageJson);
        var root = doc.RootElement;
        root.GetProperty("month").GetString().Should().Be(DateTime.UtcNow.ToString("yyyy-MM"));
        root.GetProperty("spend_brl").GetDecimal().Should().Be(5.5m);
    }

    [Fact]
    public async Task IsCategorizationBudgetAvailableAsync_RetornaFalse_AposUltrapassarLimite_ContraPostgresReal()
    {
        using var db = CreateContext();
        var service = new ClaudeBudgetService(db);

        // Limite default seedado pela migration = 30 BRL. Cada chamada de 1_000_000 tokens de
        // input custa 5.5 BRL (1 USD * 5.5) — 6 chamadas ultrapassam 30 BRL (33 BRL).
        for (var i = 0; i < 6; i++)
            await service.RecordUsageAsync(inputTokens: 1_000_000, outputTokens: 0);

        var disponivel = await service.IsCategorizationBudgetAvailableAsync();

        disponivel.Should().BeFalse();
    }
}
