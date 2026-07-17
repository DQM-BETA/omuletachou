using AfiliadoBot.Domain.Entities;
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

    public AfiliadoBotDbContext(DbContextOptions<AfiliadoBotDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AfiliadoBotDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
