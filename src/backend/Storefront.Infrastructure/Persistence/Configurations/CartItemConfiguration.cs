using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Carts;
using Storefront.Domain.Catalog;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items", table => table.HasCheckConstraint(
            "ck_cart_items_quantity",
            $"quantity BETWEEN 1 AND {Cart.MaxQuantityPerItem}"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        // One line per variant; this index also serves the foreign key to carts.
        builder.HasIndex(i => new { i.CartId, i.VariantId }).IsUnique();
        builder.HasIndex(i => i.VariantId);

        // A variant that is in a cart cannot be deleted by mistake.
        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(i => i.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
