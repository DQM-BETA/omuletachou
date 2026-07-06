using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaFieldsAndNullableAffiliateLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "affiliate_link",
                table: "products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "media_type",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "media_url",
                table: "products",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "media_type",
                table: "products");

            migrationBuilder.DropColumn(
                name: "media_url",
                table: "products");

            migrationBuilder.AlterColumn<string>(
                name: "affiliate_link",
                table: "products",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
