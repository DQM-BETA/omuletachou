using AfiliadoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfiliadoBot.Infrastructure.Data.Configurations;

public class PublicationLogConfiguration : IEntityTypeConfiguration<PublicationLog>
{
    public void Configure(EntityTypeBuilder<PublicationLog> builder)
    {
        builder.ToTable("publication_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PublicationQueueId)
            .HasColumnName("publication_queue_id")
            .IsRequired();

        builder.Property(x => x.SocialNetwork)
            .HasColumnName("social_network")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.AttemptedAt)
            .HasColumnName("attempted_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");
    }
}
