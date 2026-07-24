using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedPushVapidKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { 47, "push.vapid_public_key", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { 48, "push.vapid_private_key", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 48);
        }
    }
}
