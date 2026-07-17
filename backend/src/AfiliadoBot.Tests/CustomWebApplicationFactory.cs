using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Desliga o Hangfire nos testes de integracao via variavel de ambiente: Program.cs le
    // "Hangfire:Enabled" logo apos WebApplication.CreateBuilder(args), antes de qualquer hook
    // de ConfigureAppConfiguration do WebApplicationFactory conseguir sobrescrever a
    // configuracao (o hook so roda no momento do Build()). Variavel de ambiente de processo
    // e a unica forma garantida de chegar a tempo, pois AddEnvironmentVariables() e uma das
    // fontes padrao do WebApplication.CreateBuilder. Sem isso, AddHangfire/UsePostgreSqlStorage
    // tentaria conectar de verdade ao Postgres ("db", inexistente no host de teste).
    // Chave de teste fixa (>=256 bits), exclusiva do host de teste — nunca usada fora daqui.
    // Definida via env var pelo mesmo motivo do Hangfire__Enabled acima: Program.cs le
    // "Jwt:SigningKey" (fail-fast) logo apos WebApplication.CreateBuilder(args), antes de
    // qualquer hook ConfigureAppConfiguration do WebApplicationFactory ter chance de rodar.
    public const string TestSigningKey =
        "test-only-signing-key-32-bytes-minimo-nao-usar-fora-dos-testes-1234567890";

    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Hangfire__Enabled", "false");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "omuletachou-api");
        Environment.SetEnvironmentVariable("Jwt__Audience", "omuletachou-dashboard");
        Environment.SetEnvironmentVariable("Jwt__ExpirationHours", "24");
    }

    // Nome fixo por INSTANCIA da factory (nao por scope/request) — AddDbContext invoca a
    // optionsAction toda vez que um novo scope resolve DbContextOptions<T>; gerar o Guid
    // dentro da lambda faria cada scope apontar para um banco InMemory diferente e vazio,
    // quebrando qualquer teste que precise persistir dados em um scope (ex.: seed direto via
    // _factory.Services.CreateScope()) e lê-los em outro (via HttpClient/requisicao real).
    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AfiliadoBotDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AfiliadoBotDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
