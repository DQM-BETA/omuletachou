using AfiliadoBot.Api.RateLimiting;
using FluentAssertions;

namespace AfiliadoBot.Tests.Public;

/// <summary>
/// Testes unitarios (sem HTTP) das policies "public-read"/"public-write" (Issue #11 / Sub-D,
/// CA-D11/CA-D12/CA-E4) — validam o comportamento do limiter isoladamente, sem depender de
/// dezenas de requisicoes HTTP reais nem de tempo real decorrido.
/// </summary>
public class RateLimiterConfiguratorTests
{
    [Fact]
    public async Task PublicReadLimiter_AteSessentaRequisicoesMesmoIp_TodasAdquiremLease()
    {
        using var limiter = RateLimiterConfigurator.CreateFixedWindowLimiter(
            RateLimiterConfigurator.DefaultPublicReadPermitLimit, TimeSpan.FromMinutes(1));

        for (var i = 0; i < 60; i++)
        {
            using var lease = await limiter.AcquireAsync("1.2.3.4");
            lease.IsAcquired.Should().BeTrue($"a requisicao {i + 1} de 60 deveria estar dentro do limite");
        }
    }

    [Fact]
    public async Task PublicReadLimiter_SexagesimaPrimeiraRequisicaoMesmoIp_Rejeitada()
    {
        using var limiter = RateLimiterConfigurator.CreateFixedWindowLimiter(
            RateLimiterConfigurator.DefaultPublicReadPermitLimit, TimeSpan.FromMinutes(1));

        for (var i = 0; i < 60; i++)
        {
            using var lease = await limiter.AcquireAsync("1.2.3.4");
        }

        using var extraLease = await limiter.AcquireAsync("1.2.3.4");

        extraLease.IsAcquired.Should().BeFalse("CA-D11: a 61a requisicao no mesmo minuto/IP deve ser rejeitada (429)");
    }

    [Fact]
    public async Task PublicReadLimiter_IpDiferenteExcedido_NaoAfetaOutroIp()
    {
        using var limiter = RateLimiterConfigurator.CreateFixedWindowLimiter(
            RateLimiterConfigurator.DefaultPublicReadPermitLimit, TimeSpan.FromMinutes(1));

        for (var i = 0; i < 61; i++)
        {
            using var lease = await limiter.AcquireAsync("1.2.3.4");
        }

        using var otherIpLease = await limiter.AcquireAsync("5.6.7.8");

        otherIpLease.IsAcquired.Should().BeTrue("CA-D12: o limite e por IP, um IP distinto nao pode ser afetado");
    }

    [Fact]
    public async Task PublicWriteLimiter_AteDezRequisicoesMesmoIp_TodasAdquiremLease_DecimaPrimeiraRejeitada()
    {
        // CA-E4: policy "public-write" (10 req/min/IP) deixada pronta para a Sub-E consumir em
        // POST /api/public/push/subscribe. Validada aqui isoladamente pois o endpoint da Sub-E
        // ainda nao existe nesta sub-issue.
        using var limiter = RateLimiterConfigurator.CreateFixedWindowLimiter(
            RateLimiterConfigurator.DefaultPublicWritePermitLimit, TimeSpan.FromMinutes(1));

        for (var i = 0; i < 10; i++)
        {
            using var lease = await limiter.AcquireAsync("9.9.9.9");
            lease.IsAcquired.Should().BeTrue();
        }

        using var extraLease = await limiter.AcquireAsync("9.9.9.9");
        extraLease.IsAcquired.Should().BeFalse();
    }
}
