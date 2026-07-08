using AfiliadoBot.Api.Hangfire;
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data;
using FluentAssertions;
using global::Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests.HangfireTests;

public class HangfireAuthFilterTests
{
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

    private static DashboardContext CreateDashboardContext(AfiliadoBotDbContext dbContext, string? queryPassword)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

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
        var context = CreateDashboardContext(dbContext, queryPassword: "qualquer");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_BloqueiaAcesso_QuandoSenhaNaoConfigurada()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: null);
        var context = CreateDashboardContext(dbContext, queryPassword: "qualquer");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void Authorize_PermiteAcesso_QuandoSenhaCorreta()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: "senha-correta");
        var context = CreateDashboardContext(dbContext, queryPassword: "senha-correta");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeTrue();
    }

    [Fact]
    public void Authorize_BloqueiaAcesso_QuandoSenhaIncorreta()
    {
        using var dbContext = CreateInMemoryContext(dashboardPassword: "senha-correta");
        var context = CreateDashboardContext(dbContext, queryPassword: "senha-errada");

        var filter = new HangfireAuthFilter();

        filter.Authorize(context).Should().BeFalse();
    }
}
