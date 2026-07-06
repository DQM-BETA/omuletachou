using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceUrlToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_url",
                table: "products",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_url",
                table: "products");
        }
    }
}
