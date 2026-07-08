using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedYoutubeCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { 33, "youtube.client_id", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { 34, "youtube.client_secret", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { 35, "youtube.refresh_token", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 35);
        }
    }
}
