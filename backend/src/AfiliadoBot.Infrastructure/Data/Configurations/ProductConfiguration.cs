using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
    }
}
