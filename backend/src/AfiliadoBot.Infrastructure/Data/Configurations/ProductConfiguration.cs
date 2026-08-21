using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace AfiliadoBot.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.SalePrice)
            .HasColumnName("sale_price")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.OriginalPrice)
            .HasColumnName("original_price")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.DiscountPct)
            .HasColumnName("discount_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(x => x.AffiliateLink)
            .HasColumnName("affiliate_link")
            .HasColumnType("text");

        builder.Property(x => x.ImageUrl)
            .HasColumnName("image_url")
            .HasColumnType("text");

        builder.Property(x => x.MediaUrl)
            .HasColumnName("media_url")
            .HasColumnType("text");

        builder.Property(x => x.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(20);

        builder.Property(x => x.MediaLocalPath)
            .HasColumnName("media_local_path")
            .HasColumnType("text");

        builder.Property(x => x.SourceUrl)
            .HasColumnName("source_url")
            .HasColumnType("text");

        builder.Property(x => x.Slug)
            .HasColumnName("slug")
            .IsRequired()
            .HasMaxLength(300);

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Subcategory)
            .HasColumnName("subcategory")
            .HasMaxLength(100); // nullable — sem .IsRequired() (CA 1.1/1.2, Issue #167)

        builder.Property(x => x.Platform)
            .HasColumnName("platform")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ExternalId)
            .HasColumnName("external_id")
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.HasIndex(x => new { x.Platform, x.ExternalId })
            .IsUnique()
            .HasDatabaseName("IX_products_platform_external_id");

        // Indices compostos para os filtros publicos (Issue #167, design.md secao 4.2). "status"
        // sempre lidera (todo filtro publico comeca com Status == Published); a coluna de
        // ordenacao fica por ultimo em cada variante para o Postgres poder usar o indice tanto
        // para filtrar quanto para ja entregar os resultados ordenados.
        builder.HasIndex(x => new { x.Status, x.AiScore })
            .HasDatabaseName("IX_products_status_aiscore")
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.AiScore })
            .HasDatabaseName("IX_products_status_category_subcategory_aiscore")
            .IsDescending(false, false, false, true);

        builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.SalePrice })
            .HasDatabaseName("IX_products_status_category_subcategory_saleprice");

        builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.DiscountPct })
            .HasDatabaseName("IX_products_status_category_subcategory_discountpct")
            .IsDescending(false, false, false, true);

        builder.HasIndex(x => new { x.Status, x.Category, x.Subcategory, x.CreatedAt })
            .HasDatabaseName("IX_products_status_category_subcategory_createdat")
            .IsDescending(false, false, false, true);

        // Indice orientado a Plataforma (Issue #228, design.md secao 2.2). Os indices acima sao
        // todos orientados a Categoria/Subcategoria; nenhum cobre filtro por Plataforma isolado
        // ou combinado com faixa de data de coleta (relatorio de produtos publicados).
        builder.HasIndex(x => new { x.Status, x.Platform, x.CreatedAt })
            .HasDatabaseName("IX_products_status_platform_createdat")
            .IsDescending(false, false, true);

        builder.Property(x => x.AiScore)
            .HasColumnName("ai_score");

        builder.Property(x => x.AiReason)
            .HasColumnName("ai_reason")
            .HasMaxLength(300);

        builder.Property(x => x.AiCaption)
            .HasColumnName("ai_caption")
            .HasColumnType("text");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasMany(x => x.PublicationQueues)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Issue #231 (sub-issue #276) — click_count desnormalizado (design.md secao 4),
        // atualizado sincronamente pelo endpoint publico de clique (T-02, Product.RegisterClick).
        builder.Property(x => x.ClickCount)
            .HasColumnName("click_count")
            .HasDefaultValue(0)
            .IsRequired();

        // Indices compostos novos para a faixa de sugeridos (design.md secao 5): ranking por
        // categoria (filtro de categoria ativo) e ranking geral (fallback, sem filtro). Segue o
        // mesmo padrao dos indices acima — "status" lidera, coluna(s) de ordenacao por ultimo.
        builder.HasIndex(x => new { x.Status, x.Category, x.ClickCount, x.CreatedAt })
            .HasDatabaseName("IX_products_status_category_clickcount")
            .IsDescending(false, false, true, true);

        builder.HasIndex(x => new { x.Status, x.ClickCount, x.CreatedAt })
            .HasDatabaseName("IX_products_status_clickcount")
            .IsDescending(false, true, true);
    }

    /// <summary>
    /// Issue #260 (sub-issue #267, design.md secao 2.2/2.4, especificacao-tecnica.md secao 1):
    /// coluna gerada (GENERATED ALWAYS ... STORED) que combina titulo/categoria/descricao em um
    /// unico tsvector ponderado (A/B/C) para full-text search em portugues. Shadow property (nao
    /// exposta em Product) mapeada com o MESMO SQL aplicado via migrationBuilder.Sql na migration
    /// AddProductSearchVector — necessario para o model snapshot do EF nao tentar "corrigir"/
    /// recriar a coluna gerada em migrations futuras (CA "sem diff espurio").
    /// immutable_unaccent(text) e um wrapper IMMUTABLE proprio (criado na mesma migration) em
    /// torno de unaccent(text), que e apenas STABLE e por isso nao pode ser usada direto dentro
    /// de uma coluna gerada (design.md secao 2.4).
    /// Chamado a parte de <see cref="Configure"/> (nao dentro dele) porque o tipo Npgsql-especifico
    /// NpgsqlTsVector nao e suportado pelo provider InMemory (usado por boa parte da suite de
    /// testes, ex. CustomWebApplicationFactory) — aplicar isso incondicionalmente quebra a
    /// validacao do modelo para qualquer teste que nao use Postgres real. Chamado condicionalmente
    /// por <c>AfiliadoBotDbContext.OnModelCreating</c> (somente quando <c>Database.IsNpgsql()</c>).
    /// </summary>
    public static void ConfigureSearchVector(EntityTypeBuilder<Product> builder)
    {
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "setweight(to_tsvector('portuguese', immutable_unaccent(title)), 'A') || " +
                "setweight(to_tsvector('portuguese', immutable_unaccent(category)), 'B') || " +
                "setweight(to_tsvector('portuguese', immutable_unaccent(description)), 'C')",
                stored: true);

        builder.HasIndex("SearchVector")
            .HasDatabaseName("IX_products_search_vector")
            .HasMethod("gin");
    }
}
