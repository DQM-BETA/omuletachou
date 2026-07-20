using System.Net;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Public;

/// <summary>
/// Teste de integracao ponta a ponta (HTTP real via WebApplicationFactory) de CA-D11/CA-D12:
/// confirma que o 429 acontece de fato no pipeline completo (ForwardedHeadersMiddleware ->
/// RateLimiter), nao apenas no limiter isolado (ver RateLimiterConfiguratorTests). Usa uma
/// factory PROPRIA (nao compartilhada com PublicControllerTests/CorsTests) com um permit limit
/// baixo, configurado via "RateLimiting:PublicReadPermitLimit", para o teste rodar rapido sem
/// depender de 60+ requisicoes reais nem de tempo de janela decorrido.
/// </summary>
public class PublicDealsRateLimitIntegrationTests : IClassFixture<PublicDealsRateLimitIntegrationTests.LowLimitFactory>
{
    private const int TestPermitLimit = 5;

    private readonly LowLimitFactory _factory;

    public PublicDealsRateLimitIntegrationTests(LowLimitFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDeals_AposExcederLimitePorIp_Retorna429()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < TestPermitLimit + 1; i++)
        {
            lastResponse = await client.GetAsync("/api/public/deals");
        }

        lastResponse!.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task GetDeals_IpDiferenteExcedido_NaoAfetaOutroIp()
    {
        var exceededClient = _factory.CreateClient();
        exceededClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.2");
        for (var i = 0; i < TestPermitLimit + 1; i++)
        {
            await exceededClient.GetAsync("/api/public/deals");
        }

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.3");
        var response = await otherClient.GetAsync("/api/public/deals");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "CA-D12: IP distinto nao pode ser afetado pelo limite do outro IP");
    }

    public class LowLimitFactory : WebApplicationFactory<Program>
    {
        // Jwt/Hangfire/ForwardedHeaders precisam continuar via variavel de ambiente de PROCESSO
        // (Program.cs le esses valores de builder.Configuration de forma sincrona, ANTES de
        // Build() — ConfigureAppConfiguration so e aplicado a tempo de Build(), tarde demais para
        // esse trecho especifico). Os valores abaixo sao IDENTICOS aos de CustomWebApplicationFactory
        // (sem conflito entre factories no mesmo processo de teste).
        // O PERMIT LIMIT do rate limiter, ao contrario, e lido de forma LAZY (dentro do delegate
        // de AddRateLimiter, resolvido so quando IOptions<RateLimiterOptions> e materializado, ja
        // apos Build()) — por isso pode e DEVE vir via ConfigureAppConfiguration, escopado a esta
        // factory, para nao vazar como variavel de ambiente de processo e afetar (via corrida entre
        // testes paralelos) o limite de 60 usado pelas demais factories (CustomWebApplicationFactory)
        // no mesmo processo de teste.
        static LowLimitFactory()
        {
            Environment.SetEnvironmentVariable("Hangfire__Enabled", "false");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", CustomWebApplicationFactory.TestSigningKey);
            Environment.SetEnvironmentVariable("Jwt__Issuer", "omuletachou-api");
            Environment.SetEnvironmentVariable("Jwt__Audience", "omuletachou-dashboard");
            Environment.SetEnvironmentVariable("Jwt__ExpirationHours", "24");
            Environment.SetEnvironmentVariable("ForwardedHeaders__KnownNetworks__0", "0.0.0.0/0");
        }

        private readonly string _dbName = "TestDb_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:PublicReadPermitLimit"] = TestPermitLimit.ToString(),
                });
            });

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
}
