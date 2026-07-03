using AfiliadoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfiliadoBot.Infrastructure.Data.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Endpoint)
            .HasColumnName("endpoint")
            .IsRequired()
            .HasColumnType("text");

        builder.HasIndex(x => x.Endpoint)
            .IsUnique()
            .HasDatabaseName("IX_push_subscriptions_endpoint");

        builder.Property(x => x.P256dh)
            .HasColumnName("p256dh")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.Auth)
            .HasColumnName("auth")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
