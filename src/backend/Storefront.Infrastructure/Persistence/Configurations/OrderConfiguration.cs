using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Orders;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", table =>
        {
            table.HasCheckConstraint(
                "ck_orders_number",
                $"number BETWEEN {Order.MinNumber} AND {Order.MaxNumber}");
            table.HasCheckConstraint("ck_orders_status", "status IN ('New')");
            table.HasCheckConstraint("ck_orders_total_amount", "total_amount >= 0");
            table.HasCheckConstraint("ck_orders_shipping_amount", "shipping_amount >= 0");
            table.HasCheckConstraint("ck_orders_total_covers_shipping", "total_amount >= shipping_amount");
            table.HasCheckConstraint("ck_orders_currency", "char_length(currency) = 3");
            table.HasCheckConstraint("ck_orders_shipping_country", "char_length(shipping_country) = 2");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        builder.Property(o => o.ShippingMethod).HasMaxLength(50).IsRequired();
        builder.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Email).HasMaxLength(254).IsRequired();
        builder.Property(o => o.Phone).HasMaxLength(30).IsRequired();
        builder.Property(o => o.ShippingStreet).HasMaxLength(200).IsRequired();
        builder.Property(o => o.ShippingCity).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShippingPostalCode).HasMaxLength(20).IsRequired();
        builder.Property(o => o.ShippingCountry).HasMaxLength(2).IsRequired();
        builder.Property(o => o.TermsVersion).HasMaxLength(50).IsRequired();

        builder.Property(o => o.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(o => o.Number).IsUnique();
        builder.HasIndex(o => o.CreatedAt);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
