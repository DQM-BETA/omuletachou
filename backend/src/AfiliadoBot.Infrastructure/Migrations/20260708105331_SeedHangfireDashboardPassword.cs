using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedHangfireDashboardPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "key", "updated_at", "value" },
                values: new object[] { 32, "hangfire.dashboard_password", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 32);
        }
    }
}
