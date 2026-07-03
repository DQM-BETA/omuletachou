using AfiliadoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfiliadoBot.Infrastructure.Data.Configurations;

public class PublicationQueueConfiguration : IEntityTypeConfiguration<PublicationQueue>
{
    public void Configure(EntityTypeBuilder<PublicationQueue> builder)
    {
        builder.ToTable("publication_queue");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(x => x.SocialNetwork)
            .HasColumnName("social_network")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ScheduledAt)
            .HasColumnName("scheduled_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Ignore(x => x.CanRetry);

        builder.HasIndex(x => new { x.Status, x.ScheduledAt })
            .HasDatabaseName("IX_publication_queue_status_scheduled_at");

        builder.HasMany(x => x.Logs)
            .WithOne(x => x.PublicationQueue)
            .HasForeignKey(x => x.PublicationQueueId);
    }
}
