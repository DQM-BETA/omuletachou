using System.Net;
using System.Net.Http.Json;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.Push;

/// <summary>
/// Teste de integracao ponta a ponta (HTTP real via WebApplicationFactory) de CA-E4: confirma
/// que POST /api/public/push/subscribe aplica a policy "public-write" (fix pontual apos o merge
/// da Sub-D — ver estado.md) no pipeline completo (ForwardedHeadersMiddleware -> RateLimiter),
/// nao apenas no limiter isolado (RateLimiterConfiguratorTests.PublicWriteLimiter_*). Segue o
/// mesmo padrao de PublicDealsRateLimitIntegrationTests (Sub-D): factory propria com permit
/// limit baixo via "RateLimiting:PublicWritePermitLimit", para nao depender de 10+ requisicoes
/// reais nem de tempo de janela decorrido.
/// </summary>
public class PushSubscribeRateLimitIntegrationTests : IClassFixture<PushSubscribeRateLimitIntegrationTests.LowLimitFactory>
{
    private const int TestPermitLimit = 3;

    private readonly LowLimitFactory _factory;

    public PushSubscribeRateLimitIntegrationTests(LowLimitFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Subscribe_AposExcederLimitePorIp_Retorna429()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.0.1");

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < TestPermitLimit + 1; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/public/push/subscribe", new
            {
                endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid(),
                keys = new { p256dh = "chave-p256dh-teste", auth = "chave-auth-teste" }
            });
        }

        lastResponse!.StatusCode.Should().Be((HttpStatusCode)429, "CA-E4: a requisicao alem do limite/min/IP deve ser rejeitada");
    }

    [Fact]
    public async Task Subscribe_IpDiferenteExcedido_NaoAfetaOutroIp()
    {
        var exceededClient = _factory.CreateClient();
        exceededClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.0.2");
        for (var i = 0; i < TestPermitLimit + 1; i++)
        {
            await exceededClient.PostAsJsonAsync("/api/public/push/subscribe", new
            {
                endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid(),
                keys = new { p256dh = "chave-p256dh-teste", auth = "chave-auth-teste" }
            });
        }

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.0.3");
        var response = await otherClient.PostAsJsonAsync("/api/public/push/subscribe", new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid(),
            keys = new { p256dh = "chave-p256dh-teste", auth = "chave-auth-teste" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, "o limite e por IP, um IP distinto nao pode ser afetado");
    }

    public class LowLimitFactory : WebApplicationFactory<Program>
    {
        // Mesma justificativa de PublicDealsRateLimitIntegrationTests.LowLimitFactory: valores
        // de Jwt/Hangfire/ForwardedHeaders via variavel de ambiente de processo (lidos de forma
        // sincrona por Program.cs antes de Build()); o permit limit da policy "public-write" via
        // ConfigureAppConfiguration, escopado a esta factory, para nao vazar como env var de
        // processo e afetar o limite de 10 usado pelas demais factories no mesmo processo de teste.
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
                    ["RateLimiting:PublicWritePermitLimit"] = TestPermitLimit.ToString(),
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
