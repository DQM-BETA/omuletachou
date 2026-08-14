using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Tests.Services;

/// <summary>
/// Testes unitarios de ClaudeBudgetService sobre o provider InMemory do EF Core — cobrem a
/// logica de negocio (reset lazy mensal, calculo de custo, leitura de disponibilidade). O
/// caminho de escrita atomica via UPDATE...CASE (SQL cru, so roda contra Postgres — design.md
/// §3.5) e coberto pelo teste de integracao real contra Postgres:
/// <see cref="ClaudeBudgetServiceIntegrationTests"/> (Testcontainers).
/// </summary>
public class ClaudeBudgetServiceTests
{
    private static AfiliadoBotDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfiliadoBotDbContext(options);
    }

    private static string CurrentMonth() => DateTime.UtcNow.ToString("yyyy-MM");

    private static async Task SeedAsync(
        AfiliadoBotDbContext db,
        string? monthlyUsageJson = null,
        string budgetLimitBrl = "30",
        string priceInputUsdPerMtok = "1",
        string priceOutputUsdPerMtok = "5",
        string usdBrlRate = "5.5")
    {
        if (monthlyUsageJson is not null)
            db.AppSettings.Add(new AppSetting("claude.monthly_usage", monthlyUsageJson));

        db.AppSettings.Add(new AppSetting("claude.monthly_budget_limit_brl", budgetLimitBrl));
        db.AppSettings.Add(new AppSetting("claude.price_input_usd_per_mtok", priceInputUsdPerMtok));
        db.AppSettings.Add(new AppSetting("claude.price_output_usd_per_mtok", priceOutputUsdPerMtok));
        db.AppSettings.Add(new AppSetting("claude.usd_brl_rate", usdBrlRate));

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task IsCategorizationBudgetAvailableAsync_RetornaTrue_QuandoGastoAbaixoDoLimite()
    {
        using var db = CreateInMemoryContext();
        await SeedAsync(db, monthlyUsageJson: $"{{\"month\":\"{CurrentMonth()}\",\"spend_brl\":10}}", budgetLimitBrl: "30");

        var service = new ClaudeBudgetService(db);

        var disponivel = await service.IsCategorizationBudgetAvailableAsync();

        disponivel.Should().BeTrue();
    }

    [Fact]
    public async Task IsCategorizationBudgetAvailableAsync_RetornaFalse_QuandoGastoIgualOuAcimaDoLimite()
    {
        // CA 4.3: contador >= limite -> orcamento indisponivel.
        using var db = CreateInMemoryContext();
        await SeedAsync(db, monthlyUsageJson: $"{{\"month\":\"{CurrentMonth()}\",\"spend_brl\":30}}", budgetLimitBrl: "30");

        var service = new ClaudeBudgetService(db);

        var disponivel = await service.IsCategorizationBudgetAvailableAsync();

        disponivel.Should().BeFalse();
    }

    [Fact]
    public async Task IsCategorizationBudgetAvailableAsync_IgnoraGastoDeMesAnterior_ResetLazy()
    {
        // CA 4.5: mes salvo diferente do mes corrente -> gasto tratado como zero (reset lazy na
        // leitura, sem precisar de job/cron dedicado).
        using var db = CreateInMemoryContext();
        await SeedAsync(db, monthlyUsageJson: "{\"month\":\"2020-01\",\"spend_brl\":9999}", budgetLimitBrl: "30");

        var service = new ClaudeBudgetService(db);

        var disponivel = await service.IsCategorizationBudgetAvailableAsync();

        disponivel.Should().BeTrue();
    }

    [Fact]
    public async Task IsCategorizationBudgetAvailableAsync_RetornaTrue_QuandoChaveDeUsoAusente()
    {
        using var db = CreateInMemoryContext();
        await SeedAsync(db, monthlyUsageJson: null, budgetLimitBrl: "30");

        var service = new ClaudeBudgetService(db);

        var disponivel = await service.IsCategorizationBudgetAvailableAsync();

        disponivel.Should().BeTrue();
    }

    [Fact]
    public async Task RecordUsageAsync_IncrementaGastoDoMesCorrente()
    {
        using var db = CreateInMemoryContext();
        await SeedAsync(db, monthlyUsageJson: $"{{\"month\":\"{CurrentMonth()}\",\"spend_brl\":5}}");

        var service = new ClaudeBudgetService(db);

        // custoUSD = (1_000_000/1_000_000)*1 + (200_000/1_000_000)*5 = 1 + 1 = 2 USD
        // custoBRL = 2 * 5.5 = 11
        await service.RecordUsageAsync(inputTokens: 1_000_000, outputTokens: 200_000);

        var setting = await db.AppSettings.FirstAsync(s => s.Key == "claude.monthly_usage");
        setting.Value.Should().Contain("\"spend_brl\":16"); // 5 (seed) + 11
    }

    [Fact]
    public async Task RecordUsageAsync_ReiniciaContador_QuandoMesSalvoDivergeDoAtual()
    {
        // CA 4.5: escrita tambem participa do reset lazy — nao soma ao valor de um mes antigo.
        using var db = CreateInMemoryContext();
        await SeedAsync(db, monthlyUsageJson: "{\"month\":\"2020-01\",\"spend_brl\":9999}");

        var service = new ClaudeBudgetService(db);
        await service.RecordUsageAsync(inputTokens: 1_000_000, outputTokens: 0); // custoBRL = 1*5.5 = 5.5

        var setting = await db.AppSettings.FirstAsync(s => s.Key == "claude.monthly_usage");
        setting.Value.Should().Contain($"\"month\":\"{CurrentMonth()}\"");
        setting.Value.Should().Contain("\"spend_brl\":5.5");
    }

    [Fact]
    public async Task RecordUsageAsync_CriaRegistro_QuandoChaveDeUsoAusente()
    {
        using var db = CreateInMemoryContext();
        await SeedAsync(db, monthlyUsageJson: null);

        var service = new ClaudeBudgetService(db);
        await service.RecordUsageAsync(inputTokens: 1_000_000, outputTokens: 0);

        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "claude.monthly_usage");
        setting.Should().NotBeNull();
        setting!.Value.Should().Contain($"\"month\":\"{CurrentMonth()}\"");
    }

    [Fact]
    public async Task RecordUsageAsync_UsaDefaults_QuandoPrecoECambioAusentes()
    {
        using var db = CreateInMemoryContext();
        // Nenhuma chave de preco/cambio seedada — defaults do servico devem ser usados
        // (1 USD/MTok input, 5 USD/MTok output, 5.5 BRL/USD, mesmos valores da migration).
        var service = new ClaudeBudgetService(db);

        await service.RecordUsageAsync(inputTokens: 1_000_000, outputTokens: 0);

        var setting = await db.AppSettings.FirstAsync(s => s.Key == "claude.monthly_usage");
        setting.Value.Should().Contain("\"spend_brl\":5.5"); // (1 USD) * 5.5
    }
}
