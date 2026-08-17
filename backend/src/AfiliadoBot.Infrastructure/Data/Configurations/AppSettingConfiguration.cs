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
            new { Id = 32, Key = "hangfire.dashboard_password", Value = "", UpdatedAt = now },
            // Youtube (Issue #8 / #65): credenciais estaveis de configuracao (client_id/client_secret/
            // refresh_token) — access_token e renovado em runtime e nao e semeado aqui.
            new { Id = 33, Key = "youtube.client_id", Value = "", UpdatedAt = now },
            new { Id = 34, Key = "youtube.client_secret", Value = "", UpdatedAt = now },
            new { Id = 35, Key = "youtube.refresh_token", Value = "", UpdatedAt = now },
            // Instagram (Issue #9 / #73): instagram.access_token (Id 16) e instagram.page_id
            // (Id 17) ja seeded desde a Issue #2 — completando com as chaves adicionais exigidas
            // pelo fluxo de renovacao de token (fb_exchange_token) e pela URL publica de midia.
            new { Id = 36, Key = "instagram.app_id", Value = "", UpdatedAt = now },
            new { Id = 37, Key = "instagram.app_secret", Value = "", UpdatedAt = now },
            new { Id = 38, Key = "instagram.token_expires_at", Value = "", UpdatedAt = now },
            new { Id = 39, Key = "instagram.token_invalid", Value = "false", UpdatedAt = now },
            new { Id = 40, Key = "api.public_base_url", Value = "", UpdatedAt = now },
            // Ids 41-50 ja usados por migrations de seed anteriores (SeedTikTokCredentials,
            // SeedPushVapidKeys, SeedFacebookCredentials — Ids 41-46/47-48/49-50 respectivamente)
            // via InsertData direto na migration, sem atualizar este HasData/o model snapshot
            // (divergencia pre-existente entre o historico real de migrations e o modelo
            // declarativo — registrada em .claude/melhorias na Issue #167/#168). Por isso os
            // seeds novos abaixo comecam em 51, nao 41, para nao colidir com linhas ja inseridas
            // em bancos existentes (confirmado rodando a migration contra Postgres real).
            //
            // Orcamento mensal de fallback de categorizacao via Claude (Issue #167 — CA 4.1).
            // Preco/cambio sao placeholders "soft guard" (design.md §8): Gerente/DevOps confirma
            // a tabela de precos vigente da Anthropic para claude-haiku-4-5-20251001 antes do
            // deploy; nao bloqueante ate la (fallback so roda quando implementado na Sub-B/#169).
            new { Id = 51, Key = "claude.monthly_budget_limit_brl", Value = "30", UpdatedAt = now },
            new { Id = 52, Key = "claude.monthly_usage", Value = "{\"month\":\"\",\"spend_brl\":0}", UpdatedAt = now },
            new { Id = 53, Key = "claude.price_input_usd_per_mtok", Value = "1", UpdatedAt = now },
            new { Id = 54, Key = "claude.price_output_usd_per_mtok", Value = "5", UpdatedAt = now },
            new { Id = 55, Key = "claude.usd_brl_rate", Value = "5.5", UpdatedAt = now }
        );
    }
}
