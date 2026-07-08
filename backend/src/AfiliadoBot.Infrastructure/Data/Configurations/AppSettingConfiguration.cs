using AfiliadoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfiliadoBot.Infrastructure.Data.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("app_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Key)
            .HasColumnName("key")
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(x => x.Key)
            .IsUnique()
            .HasDatabaseName("IX_app_settings_key");

        builder.Property(x => x.Value)
            .HasColumnName("value")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Seed data — 30 registros
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new { Id = 1, Key = "amazon.access_key", Value = "", UpdatedAt = now },
            new { Id = 2, Key = "amazon.secret_key", Value = "", UpdatedAt = now },
            new { Id = 3, Key = "amazon.partner_tag", Value = "", UpdatedAt = now },
            new { Id = 4, Key = "amazon.marketplace", Value = "", UpdatedAt = now },
            new { Id = 5, Key = "mercadolivre.access_token", Value = "", UpdatedAt = now },
            new { Id = 6, Key = "mercadolivre.refresh_token", Value = "", UpdatedAt = now },
            new { Id = 7, Key = "mercadolivre.client_id", Value = "", UpdatedAt = now },
            new { Id = 8, Key = "mercadolivre.client_secret", Value = "", UpdatedAt = now },
            new { Id = 9, Key = "shopee.partner_id", Value = "", UpdatedAt = now },
            new { Id = 10, Key = "shopee.partner_key", Value = "", UpdatedAt = now },
            new { Id = 11, Key = "shopee.shop_id", Value = "", UpdatedAt = now },
            new { Id = 12, Key = "telegram.bot_token", Value = "", UpdatedAt = now },
            new { Id = 13, Key = "telegram.channel_id", Value = "", UpdatedAt = now },
            new { Id = 14, Key = "youtube.api_key", Value = "", UpdatedAt = now },
            new { Id = 15, Key = "youtube.channel_id", Value = "", UpdatedAt = now },
            new { Id = 16, Key = "instagram.access_token", Value = "", UpdatedAt = now },
            new { Id = 17, Key = "instagram.page_id", Value = "", UpdatedAt = now },
            new { Id = 18, Key = "tiktok.access_token", Value = "", UpdatedAt = now },
            new { Id = 19, Key = "tiktok.open_id", Value = "", UpdatedAt = now },
            new { Id = 20, Key = "claude.api_key", Value = "", UpdatedAt = now },
            new { Id = 21, Key = "claude.model", Value = "", UpdatedAt = now },
            new { Id = 22, Key = "claude.min_score", Value = "6", UpdatedAt = now },
            new { Id = 23, Key = "schedule.collector_cron", Value = "0 6 * * *", UpdatedAt = now },
            new { Id = 24, Key = "schedule.publisher_cron", Value = "0 9,12,15,18,20 * * *", UpdatedAt = now },
            new { Id = 25, Key = "publish.max_per_day", Value = "10", UpdatedAt = now },
            new { Id = 26, Key = "networks.telegram.enabled", Value = "true", UpdatedAt = now },
            new { Id = 27, Key = "networks.youtube.enabled", Value = "true", UpdatedAt = now },
            new { Id = 28, Key = "networks.instagram.enabled", Value = "true", UpdatedAt = now },
            new { Id = 29, Key = "networks.tiktok.enabled", Value = "true", UpdatedAt = now },
            new { Id = 30, Key = "networks.facebook.enabled", Value = "true", UpdatedAt = now },
            new { Id = 31, Key = "claude.min_score_fallback", Value = "5", UpdatedAt = now },
            new { Id = 32, Key = "hangfire.dashboard_password", Value = "", UpdatedAt = now }
        );
    }
}
