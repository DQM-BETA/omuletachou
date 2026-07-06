using System;
using AfiliadoBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AfiliadoBotDbContext))]
    [Migration("20240102000000_AddClaudeMinScoreFallbackSeed")]
    public partial class AddClaudeMinScoreFallbackSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "key", "value", "updated_at" },
                values: new object[] { 31, "claude.min_score_fallback", "5", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 31);
        }
    }
}
