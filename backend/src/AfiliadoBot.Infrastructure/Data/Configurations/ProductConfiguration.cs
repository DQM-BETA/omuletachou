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
