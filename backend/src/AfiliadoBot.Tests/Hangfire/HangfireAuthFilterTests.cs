using System.Net;
using AfiliadoBot.Api.Hangfire;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using global::Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.HangfireTests;

public class HangfireAuthFilterTests : IDisposable
{
    public HangfireAuthFilterTests()
    {
        // Isola cada teste do estado estatico de lockout (contador em memoria por IP).
        HangfireAuthFilter.ResetLockoutStateForTests();
    }

    public void Dispose()
    {
        HangfireAuthFilter.ResetLockoutStateForTests();
    }

    private static AfiliadoBotDbContext CreateInMemoryContext(string? dashboardPassword)
    {
        var options = new DbContextOptionsBuilder<AfiliadoBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new AfiliadoBotDbContext(options);

        if (dashboardPassword is not null)
        {
            dbContext.AppSettings.Add(new AppSetting("hangfire.dashboard_password", dashboardPassword));
            dbContext.SaveChanges();
        }

        return dbContext;
    }

    private static DashboardContext CreateDashboardContext(
        AfiliadoBotDbContext dbContext, string? queryPassword, string? remoteIp = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        if (remoteIp is not null)
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        }

        if (queryPassword is not null)
        {
            httpContext.Request.QueryString = new QueryString($"?password={queryPassword}");
        }

        return new AspNetCoreDashboardContext(NoOpJobStorage.Instance, new global::Hangfire.DashboardOptions(), httpContext);
    }

    [Fact]
    public void Authorize_BloqueiaAcesso_QuandoSenhaVazia()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: "");
        var context = CreateDashboardContext(dbContext, queryPassword: "qualquer", remoteIp: "10.0.0.1");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_BloqueiaAcesso_QuandoSenhaNaoConfigurada()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: null);
        var context = CreateDashboardContext(dbContext, queryPassword: "qualquer", remoteIp: "10.0.0.2");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_PermiteAcesso_QuandoSenhaCorreta()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: "senha-correta");
        var context = CreateDashboardContext(dbContext, queryPassword: "senha-correta", remoteIp: "10.0.0.3");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeTrue();
    }

    [Fact]
    public void Authorize_BloqueiaAcesso_QuandoSenhaIncorreta()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: "senha-correta");
        var context = CreateDashboardContext(dbContext, queryPassword: "senha-errada", remoteIp: "10.0.0.4");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_PermiteAcesso_QuandoSenhaComTamanhoDiferenteDaConfigurada()
    {
        // Regressao do fix de timing-safe: senhas de tamanhos diferentes nao podem lancar
        // excecao nem quebrar a comparacao (FixedTimeEquals exige buffers do mesmo tamanho —
        // por isso a implementacao hasheia antes de comparar).
        using var dbContext = CreateInMemoryContext(dashboardPassword: "senha-correta-bem-longa");
        var context = CreateDashboardContext(dbContext, queryPassword: "x", remoteIp: "10.0.0.5");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_BloqueiaAposCincoTentativasErradas_MesmoComSenhaCorretaNaSexta()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: "senha-correta");
        var filter = new HangfireAuthFilter();
        const string ip = "10.0.0.6";

        for (var i = 0; i < HangfireAuthFilter.MaxAttempts; i++)
        {
            var attemptContext = CreateDashboardContext(dbContext, queryPassword: "senha-errada", remoteIp: ip);
            filter.Authorize(attemptContext).Should().BeFalse();
        }

        var sixthAttempt = CreateDashboardContext(dbContext, queryPassword: "senha-correta", remoteIp: ip);
        filter.Authorize(sixthAttempt).Should().BeFalse("apos 5 tentativas erradas, o IP fica bloqueado ate a janela expirar");
    }

    [Fact]
    public void Authorize_NaoAfetaOutroIp_QuandoUmIpEstaBloqueado()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: "senha-correta");
        var filter = new HangfireAuthFilter();
        const string ipBloqueado = "10.0.0.7";
        const string outroIp = "10.0.0.8";

        for (var i = 0; i < HangfireAuthFilter.MaxAttempts; i++)
        {
            var attemptContext = CreateDashboardContext(dbContext, queryPassword: "senha-errada", remoteIp: ipBloqueado);
            filter.Authorize(attemptContext).Should().BeFalse();
        }

        var otherContext = CreateDashboardContext(dbContext, queryPassword: "senha-correta", remoteIp: outroIp);
        filter.Authorize(otherContext).Should().BeTrue("o lockout e por IP, um IP distinto nao pode ser afetado");
    }
}
