using System.Net;
using System.Net.Http.Headers;
using AfiliadoBot.Api.Cors;
using FluentAssertions;

namespace AfiliadoBot.Tests.Public;

/// <summary>
/// Cobre CA-D8/CA-D9/CA-D10 (CORS restrito a lista explicita de origins) — Issue #11 / Sub-D.
/// Usa uma factory propria (nao compartilhada com PublicControllerTests) para nao interferir no
/// contador do RateLimiter de outros testes.
/// </summary>
public class CorsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CorsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("https://omuletachou.com.br")]
    [InlineData("https://www.omuletachou.com.br")]
    [InlineData("https://dashboard.omuletachou.com.br")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:4200")]
    public async Task Preflight_OrigemAutorizada_RespondeComAccessControlAllowOrigin(string origin)
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/public/deals");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().BeTrue();
        values!.Should().Contain(origin);
    }

    [Fact]
    public async Task Preflight_OrigemNaoAutorizada_NaoRetornaHeaderDeLiberacao()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/public/deals");
        request.Headers.Add("Origin", "https://site-malicioso.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out _).Should().BeFalse();
    }

    [Fact]
    public void ConfiguracaoDeCors_NuncaUsaAllowAnyOrigin_ListaEExplicita()
    {
        // Reforca CA-D10 no nivel de configuracao: os origins default (usados quando
        // "Cors:AllowedOrigins" nao esta definido) sao uma lista fechada de 5 entradas
        // especificas, nunca um wildcard.
        CorsConfigurator.DefaultAllowedOrigins.Should().HaveCount(5);
        CorsConfigurator.DefaultAllowedOrigins.Should().NotContain("*");
    }
}
