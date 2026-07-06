using AfiliadoBot.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfiliadoBot.Tests;

/// <summary>
/// Factory de teste que substitui o AfiliadoBotDbContext (Npgsql) por EF Core InMemory,
/// para que o host suba (incluindo Database.Migrate() no startup) sem depender de um
/// Postgres real. Migrate() é no-op para o provider InMemory.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AfiliadoBotDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AfiliadoBotDbContext>(options =>
                options.UseInMemoryDatabase("HealthCheckTestsDb"));
        });
    }
}
