using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", table =>
        {
            table.HasCheckConstraint("ck_order_items_quantity", "quantity >= 1");
            table.HasCheckConstraint("ck_order_items_unit_price", "unit_price >= 0");
            table.HasCheckConstraint("ck_order_items_line_total", "line_total = unit_price * quantity");
            table.HasCheckConstraint(
                "ck_order_items_fulfilment_type",
                "fulfilment_type IN ('InStock', 'MadeToOrder')");
            table.HasCheckConstraint(
                "ck_order_items_lead_time_positive",
                "lead_time_days IS NULL OR lead_time_days > 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.VariantName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Sku).HasMaxLength(100).IsRequired();
        builder.Property(i => i.FulfilmentType).HasConversion<string>().HasMaxLength(20);

        // No unique index on (order, variant): the lines are copies of what was sold; the service merges lines.
        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.VariantId);

        // A variant that was sold cannot be deleted by mistake; the order keeps its copy either way.
        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(i => i.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
