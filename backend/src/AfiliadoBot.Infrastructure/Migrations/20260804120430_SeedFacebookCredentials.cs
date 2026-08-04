using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedFacebookCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Item A5 (Issue #133 / #145): facebook.access_token/facebook.page_id nunca foram
            // inseridos em app_settings, embora networks.facebook.enabled ja exista (id 30,
            // InitialSchema) e ProcessorJob.NetworkSettings exija ambas as chaves via
            // HasCredentials para qualificar a rede Facebook. Mesmo padrao de
            // SeedInstagramCredentials/SeedYoutubeCredentials — proximos ids livres 49/50
            // (maior id usado ate SeedTikTokCredentials/AddUsersTable/SeedPushVapidKeys: 48).
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { 49, "facebook.access_token", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { 50, "facebook.page_id", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 50);
        }
    }
}
