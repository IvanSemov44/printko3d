using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Shipping;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class ShippingRateConfiguration : IEntityTypeConfiguration<ShippingRate>
{
    public void Configure(EntityTypeBuilder<ShippingRate> builder)
    {
        builder.ToTable("shipping_rates", table =>
        {
            table.HasCheckConstraint("ck_shipping_rates_max_weight_grams", "max_weight_grams > 0");
            table.HasCheckConstraint("ck_shipping_rates_price", "price >= 0");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.MethodCode).HasMaxLength(50).IsRequired();

        // Two steps of one method with the same weight would make the price depend on the order of reading.
        builder.HasIndex(r => new { r.MethodCode, r.MaxWeightGrams }).IsUnique();
    }
}
