using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductClicksAndClickCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "click_count",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "product_clicks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clicked_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_clicks", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_clicks_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_status_category_clickcount",
                table: "products",
                columns: new[] { "status", "category", "click_count", "created_at" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_products_status_clickcount",
                table: "products",
                columns: new[] { "status", "click_count", "created_at" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_product_clicks_product_id",
                table: "product_clicks",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_clicks");

            migrationBuilder.DropIndex(
                name: "IX_products_status_category_clickcount",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_status_clickcount",
                table: "products");

            migrationBuilder.DropColumn(
                name: "click_count",
                table: "products");
        }
    }
}
