using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_runs_job_name_started_at",
                table: "job_runs",
                columns: new[] { "job_name", "started_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_runs");
        }
    }
}
