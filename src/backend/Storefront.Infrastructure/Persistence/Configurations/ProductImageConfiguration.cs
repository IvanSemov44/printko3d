using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Catalog;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(i => i.AltText).HasMaxLength(300).IsRequired();

        builder.HasIndex(i => i.ProductId)
            .IsUnique()
            .HasFilter("is_primary")
            .HasDatabaseName("ux_product_images_one_primary_per_product");
        builder.HasIndex(i => i.VariantId);
        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(i => i.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
