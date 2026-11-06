using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Catalog;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants", table =>
        {
            table.HasCheckConstraint("ck_product_variants_price", "price >= 0");
            table.HasCheckConstraint(
                "ck_product_variants_compare_at_price",
                "compare_at_price IS NULL OR compare_at_price >= 0");
            table.HasCheckConstraint(
                "ck_product_variants_stock_quantity",
                "stock_quantity IS NULL OR stock_quantity >= 0");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Sku).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Attributes)
            .HasColumnType("jsonb")
            .HasConversion(
                attributes => JsonSerializer.Serialize(attributes, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null)
                    ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (left, right) => left!.Count == right!.Count && !left.Except(right).Any(),
                    attributes => attributes.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    attributes => new Dictionary<string, string>(attributes)))
            .IsRequired();

        builder.HasIndex(v => v.Sku).IsUnique();
        builder.HasIndex(v => v.ProductId);
    }
}
