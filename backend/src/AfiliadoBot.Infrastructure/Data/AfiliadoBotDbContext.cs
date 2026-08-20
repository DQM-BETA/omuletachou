using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AfiliadoBot.Infrastructure.Data;

public class AfiliadoBotDbContext : DbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<PublicationQueue> PublicationQueues { get; set; } = null!;
    public DbSet<AppSetting> AppSettings { get; set; } = null!;
    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
    public DbSet<PublicationLog> PublicationLogs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<JobRun> JobRuns { get; set; } = null!;

    public AfiliadoBotDbContext(DbContextOptions<AfiliadoBotDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AfiliadoBotDbContext).Assembly);

        // Issue #260 (sub-issue #267): search_vector (tsvector gerado + indice GIN) usa o tipo
        // Npgsql-especifico NpgsqlTsVector, que o provider InMemory (usado por boa parte da suite
        // de testes que nao precisa de Postgres real, ex. CustomWebApplicationFactory) nao sabe
        // mapear. So configurado quando o provider ativo do DbContext e o Npgsql.
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<Product>(ProductConfiguration.ConfigureSearchVector);
        }

        base.OnModelCreating(modelBuilder);
    }
}
