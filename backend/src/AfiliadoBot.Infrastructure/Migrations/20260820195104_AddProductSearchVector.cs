using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace AfiliadoBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #260 (sub-issue #267, especificacao-tecnica.md secao 1, design.md secao 2.4):
            // extensoes contrib do Postgres necessarias para a busca textual — unaccent (variacao
            // de acento) e pg_trgm (fuzzy/similarity, usado pelo estagio 2 do endpoint de busca,
            // sub-issue #268). Ambas ja incluidas na imagem postgres:16.14-alpine em uso, sem
            // mudanca de infraestrutura.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // unaccent(text) e apenas STABLE, nao IMMUTABLE — o Postgres recusa usa-la direto numa
            // expressao de coluna gerada (GENERATED ALWAYS AS (...) STORED exige IMMUTABLE). Este
            // wrapper e o padrao documentado da comunidade Postgres para esse problema.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION immutable_unaccent(text) RETURNS text AS $$
  SELECT unaccent('unaccent', $1)
$$ LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT;
");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "products",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('portuguese', immutable_unaccent(title)), 'A') || setweight(to_tsvector('portuguese', immutable_unaccent(category)), 'B') || setweight(to_tsvector('portuguese', immutable_unaccent(description)), 'C')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_search_vector",
                table: "products",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_search_vector",
                table: "products");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "products");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS immutable_unaccent(text);");

            // Extensoes deliberadamente NAO removidas no Down: unaccent/pg_trgm sao contrib do
            // Postgres sem custo de manutencao e outros objetos podem passar a depender delas
            // (ex. pg_trgm no estagio 2 da busca, sub-issue #268) — dropar extensao em rollback
            // arrisca quebrar algo nao relacionado a esta migration especifica.
        }
    }
}
