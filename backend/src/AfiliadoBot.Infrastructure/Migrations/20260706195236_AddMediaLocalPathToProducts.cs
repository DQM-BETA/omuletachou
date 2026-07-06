using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaLocalPathToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "media_local_path",
                table: "products",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "media_local_path",
                table: "products");
        }
    }
}
