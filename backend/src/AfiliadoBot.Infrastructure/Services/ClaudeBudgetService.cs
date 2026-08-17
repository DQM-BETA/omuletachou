using System.Globalization;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Infrastructure.Services;

/// <summary>
/// Implementacao de IClaudeBudgetService (Issue #167 — Sub-B/#169, design.md §3, especificacao-
/// tecnica.md §6). Chaves e defaults ja seedados pela migration da Sub-A/#168
/// (AddSubcategoryAndCategorizationBudget, Ids 51-55).
/// </summary>
public class ClaudeBudgetService : IClaudeBudgetService
{
    internal const string MonthlyUsageKey = "claude.monthly_usage";
    internal const string MonthlyBudgetLimitKey = "claude.monthly_budget_limit_brl";
    internal const string PriceInputKey = "claude.price_input_usd_per_mtok";
    internal const string PriceOutputKey = "claude.price_output_usd_per_mtok";
    internal const string UsdBrlRateKey = "claude.usd_brl_rate";

    private const decimal DefaultBudgetLimitBrl = 30m;
    private const decimal DefaultPriceInputUsdPerMtok = 1m;
    private const decimal DefaultPriceOutputUsdPerMtok = 5m;
    private const decimal DefaultUsdBrlRate = 5.5m;

    private readonly AfiliadoBotDbContext _dbContext;

    public ClaudeBudgetService(AfiliadoBotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsCategorizationBudgetAvailableAsync(CancellationToken ct = default)
    {
        var usageRaw = await _dbContext.AppSettings
            .Where(s => s.Key == MonthlyUsageKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        var limitRaw = await _dbContext.AppSettings
            .Where(s => s.Key == MonthlyBudgetLimitKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        var limit = ParseDecimal(limitRaw, DefaultBudgetLimitBrl);
        var spend = ParseUsage(usageRaw).SpendForCurrentMonth();

        return spend < limit;
    }

    public async Task RecordUsageAsync(int inputTokens, int outputTokens, CancellationToken ct = default)
    {
        var deltaBrl = await CalculateCostBrlAsync(inputTokens, outputTokens, ct);
        var currentMonth = CurrentMonth();

        // Postgres (producao/homolog): UPDATE...CASE de uma unica linha e atomico (lock de linha
        // serializa concorrencia), evitando lost-update entre execucoes concorrentes do
        // ProcessorJob (design.md §3.5). Reset lazy embutido no mesmo statement: mes diferente do
        // salvo -> reinicializa o JSON em vez de somar ao valor antigo.
        if (_dbContext.Database.IsNpgsql())
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE app_settings
                SET value = CASE
                        WHEN (value::jsonb->>'month') = {currentMonth}
                            THEN jsonb_set(value::jsonb, '{{spend_brl}}',
                                 to_jsonb(((value::jsonb->>'spend_brl')::numeric + {deltaBrl})))::text
                        ELSE jsonb_build_object('month', {currentMonth}, 'spend_brl', {deltaBrl})::text
                    END,
                    updated_at = now()
                WHERE key = {MonthlyUsageKey};", ct);
            return;
        }

        // Fallback nao-atomico: usado apenas por providers sem suporte a SQL cru (ex.: o
        // provider InMemory do EF Core, usado pelos testes unitarios que nao precisam validar
        // concorrencia real). O caminho de producao (acima) e o unico que roda contra Postgres, e
        // e a atomicidade dele que e validada pelo teste de integracao real (Testcontainers,
        // ClaudeBudgetServiceIntegrationTests) — nunca este caminho de fallback.
        var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == MonthlyUsageKey, ct);
        if (setting is null)
        {
            _dbContext.AppSettings.Add(new AppSetting(MonthlyUsageKey, BuildUsageJson(currentMonth, deltaBrl)));
        }
        else
        {
            var usage = ParseUsage(setting.Value);
            var newSpend = usage.SpendForCurrentMonth() + deltaBrl;
            setting.UpdateValue(BuildUsageJson(currentMonth, newSpend));
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<decimal> CalculateCostBrlAsync(int inputTokens, int outputTokens, CancellationToken ct)
    {
        var keys = new[] { PriceInputKey, PriceOutputKey, UsdBrlRateKey };
        var settings = await _dbContext.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var priceInput = ParseDecimal(settings.GetValueOrDefault(PriceInputKey), DefaultPriceInputUsdPerMtok);
        var priceOutput = ParseDecimal(settings.GetValueOrDefault(PriceOutputKey), DefaultPriceOutputUsdPerMtok);
        var usdBrlRate = ParseDecimal(settings.GetValueOrDefault(UsdBrlRateKey), DefaultUsdBrlRate);

        var costUsd = (inputTokens / 1_000_000m) * priceInput + (outputTokens / 1_000_000m) * priceOutput;
        return costUsd * usdBrlRate;
    }

    private static string CurrentMonth() => DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string BuildUsageJson(string month, decimal spendBrl) =>
        JsonSerializer.Serialize(new { month, spend_brl = spendBrl });

    private static UsageSnapshot ParseUsage(string? usageJson)
    {
        if (string.IsNullOrWhiteSpace(usageJson))
            return new UsageSnapshot(string.Empty, 0m);

        try
        {
            using var doc = JsonDocument.Parse(usageJson);
            var root = doc.RootElement;

            var month = root.TryGetProperty("month", out var monthProp) ? monthProp.GetString() ?? string.Empty : string.Empty;
            var spend = root.TryGetProperty("spend_brl", out var spendProp) ? spendProp.GetDecimal() : 0m;

            return new UsageSnapshot(month, spend);
        }
        catch (JsonException)
        {
            return new UsageSnapshot(string.Empty, 0m);
        }
    }

    private static decimal ParseDecimal(string? raw, decimal fallback)
    {
        return !string.IsNullOrWhiteSpace(raw) &&
               decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private readonly record struct UsageSnapshot(string Month, decimal SpendBrl)
    {
        /// <summary>
        /// Reset lazy (CA 4.5): mes salvo diferente do mes corrente -> gasto tratado como zero,
        /// sem escrever nada aqui (a escrita real de reset acontece em RecordUsageAsync).
        /// </summary>
        public decimal SpendForCurrentMonth() =>
            string.Equals(Month, CurrentMonth(), StringComparison.Ordinal) ? SpendBrl : 0m;
    }
}
