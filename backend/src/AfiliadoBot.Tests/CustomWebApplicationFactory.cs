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
    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Hangfire__Enabled", "false");
    }

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
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
        });
    }
}
