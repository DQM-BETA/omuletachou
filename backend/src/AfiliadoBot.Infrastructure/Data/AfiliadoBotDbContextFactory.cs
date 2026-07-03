using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AfiliadoBot.Infrastructure.Data;

/// <summary>
/// Design-time factory para suporte a dotnet ef migrations sem startup project.
/// </summary>
public class AfiliadoBotDbContextFactory : IDesignTimeDbContextFactory<AfiliadoBotDbContext>
{
    public AfiliadoBotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AfiliadoBotDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=afiliadoBot;Username=postgres;Password=postgres");

        return new AfiliadoBotDbContext(optionsBuilder.Options);
    }
}
