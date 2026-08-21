using AfiliadoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfiliadoBot.Infrastructure.Data.Configurations;

/// <summary>
/// Issue #231 (sub-issue #276) — mapeamento da tabela product_clicks (tabela de eventos,
/// append-only, design.md secao 4). Descoberta automaticamente por
/// ApplyConfigurationsFromAssembly (AfiliadoBotDbContext.OnModelCreating).
/// </summary>
public class ProductClickConfiguration : IEntityTypeConfiguration<ProductClick>
{
    public void Configure(EntityTypeBuilder<ProductClick> builder)
    {
        builder.ToTable("product_clicks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(x => x.ClickedAt)
            .HasColumnName("clicked_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => x.ProductId)
            .HasDatabaseName("IX_product_clicks_product_id");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
