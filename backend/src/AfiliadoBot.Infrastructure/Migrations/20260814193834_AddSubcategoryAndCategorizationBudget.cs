using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcategoryAndCategorizationBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "subcategory",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { 51, "claude.monthly_budget_limit_brl", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "30" },
                    { 52, "claude.monthly_usage", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{\"month\":\"\",\"spend_brl\":0}" },
                    { 53, "claude.price_input_usd_per_mtok", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1" },
                    { 54, "claude.price_output_usd_per_mtok", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "5" },
                    { 55, "claude.usd_brl_rate", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "5.5" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_status_aiscore",
                table: "products",
                columns: new[] { "status", "ai_score" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_products_status_category_subcategory_aiscore",
                table: "products",
                columns: new[] { "status", "category", "subcategory", "ai_score" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_products_status_category_subcategory_createdat",
                table: "products",
                columns: new[] { "status", "category", "subcategory", "created_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_products_status_category_subcategory_discountpct",
                table: "products",
                columns: new[] { "status", "category", "subcategory", "discount_pct" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_products_status_category_subcategory_saleprice",
                table: "products",
                columns: new[] { "status", "category", "subcategory", "sale_price" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_status_aiscore",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_status_category_subcategory_aiscore",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_status_category_subcategory_createdat",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_status_category_subcategory_discountpct",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_status_category_subcategory_saleprice",
                table: "products");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: 55);

            migrationBuilder.DropColumn(
                name: "subcategory",
                table: "products");
        }
    }
}
