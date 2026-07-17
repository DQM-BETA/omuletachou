using AfiliadoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfiliadoBot.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
