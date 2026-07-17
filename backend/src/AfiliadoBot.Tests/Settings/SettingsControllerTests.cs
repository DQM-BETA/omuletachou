using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using AfiliadoBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Settings;

/// <summary>
/// Cobre CA-C1 a CA-C6 e CA-C10 (Issue #11 / Sub-C) — mascaramento de secrets em
/// GET/PUT /api/settings, com foco especial no requisito mais critico desta sub-issue:
/// o valor completo de uma chave sensivel NUNCA pode aparecer em nenhuma resposta JSON.
/// </summary>
public class SettingsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SettingsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestTokenFactory.CreateValidToken());
        return client;
    }

    private async Task SeedSettingAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();

        var existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing is not null)
        {
            existing.UpdateValue(value);
        }
        else
        {
            db.AppSettings.Add(new AppSetting(key, value));
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAll_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_ChaveSensivelMascaraUltimos4Caracteres_FormatoExato()
    {
        const string secretValue = "sk-live-abcdef1234567890a1b2";
        await SeedSettingAsync("telegram.bot_token", secretValue);

        var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();
        var settings = JsonSerializer.Deserialize<JsonElement>(raw);

        var telegramToken = settings.EnumerateArray()
            .First(s => s.GetProperty("key").GetString() == "telegram.bot_token");

        telegramToken.GetProperty("value").GetString().Should().Be("****************a1b2");

        // Requisito mais critico da sub-issue: o valor completo NUNCA aparece na resposta.
        raw.Should().NotContain(secretValue);
    }

    [Fact]
    public async Task GetAll_ChaveNaoSensivel_NaoMascara()
    {
        await SeedSettingAsync("claude.min_score", "7");

        var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/settings");

        var raw = await response.Content.ReadAsStringAsync();
        var settings = JsonSerializer.Deserialize<JsonElement>(raw);

        var minScore = settings.EnumerateArray()
            .First(s => s.GetProperty("key").GetString() == "claude.min_score");

        minScore.GetProperty("value").GetString().Should().Be("7");
    }

    [Fact]
    public async Task GetAll_ChaveSensivelSemValorConfigurado_RetornaNull_NaoMascaraStringVazia()
    {
        await SeedSettingAsync("shopee.partner_key", "");

        var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/settings");

        var raw = await response.Content.ReadAsStringAsync();
        var settings = JsonSerializer.Deserialize<JsonElement>(raw);

        var partnerKey = settings.EnumerateArray()
            .First(s => s.GetProperty("key").GetString() == "shopee.partner_key");

        partnerKey.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_ChaveExistente_SobrescreveValorIntegralmente()
    {
        await SeedSettingAsync("claude.min_score", "5");

        var client = AuthenticatedClient();
        var response = await client.PutAsJsonAsync("/api/settings/claude.min_score", new { value = "9" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var updated = await db.AppSettings.FirstAsync(s => s.Key == "claude.min_score");
        updated.Value.Should().Be("9");
    }

    [Fact]
    public async Task Put_ChaveInexistente_Retorna404_NaoCriaImplicitamente()
    {
        var client = AuthenticatedClient();

        var response = await client.PutAsJsonAsync("/api/settings/chave.que.nao.existe", new { value = "qualquer" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var created = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "chave.que.nao.existe");
        created.Should().BeNull();
    }

    [Fact]
    public async Task Put_ChaveSensivel_NuncaRetornaValorCompletoNoCorpoDaResposta()
    {
        const string oldValue = "old-secret-value-0000";
        const string newValue = "novo-valor-secreto-super-longo-9999";
        await SeedSettingAsync("telegram.bot_token", oldValue);

        var client = AuthenticatedClient();
        var response = await client.PutAsJsonAsync("/api/settings/telegram.bot_token", new { value = newValue });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();

        // CA-C6: nem o novo, nem o antigo valor completo pode aparecer na resposta.
        raw.Should().NotContain(oldValue);
        raw.Should().NotContain(newValue);

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        body.GetProperty("value").GetString().Should().Be("****************9999");

        // Confirma que o valor foi de fato persistido integralmente no banco (CA-C4),
        // apesar de nunca ter sido ecoado por completo na resposta HTTP.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AfiliadoBotDbContext>();
        var persisted = await db.AppSettings.FirstAsync(s => s.Key == "telegram.bot_token");
        persisted.Value.Should().Be(newValue);
    }

    [Fact]
    public async Task Put_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/settings/claude.min_score", new { value = "1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
