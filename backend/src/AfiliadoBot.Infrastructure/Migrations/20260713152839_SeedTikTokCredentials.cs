using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedTikTokCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { 41, "tiktok.client_key", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { 42, "tiktok.client_secret", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { 43, "tiktok.refresh_token", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { 44, "tiktok.privacy_level", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SELF_ONLY" },
                    { 45, "tiktok.min_duration_seconds", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "3" },
                    { 46, "tiktok.max_duration_seconds", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "600" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 46);
        }
    }
}
