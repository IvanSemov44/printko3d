using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Payments;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public const string OneOpenPaymentPerOrderIndex = "ux_payments_one_open_payment_per_order";

    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", table =>
        {
            table.HasCheckConstraint("ck_payments_amount", "amount > 0");
            table.HasCheckConstraint("ck_payments_currency", "char_length(currency) = 3");
            table.HasCheckConstraint(
                "ck_payments_status",
                "status IN ('Pending', 'Succeeded', 'Failed', 'PaidWithoutOrder')");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Provider).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ProviderReference).HasMaxLength(200).IsRequired();
        builder.Property(p => p.RedirectUrl).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(p => p.Order)
            .WithMany()
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.HasIndex(p => new { p.Provider, p.ProviderReference }).IsUnique();

        // A buyer has at most one session waiting and at most one that paid; a failed one does not block a new try.
        builder.HasIndex(p => p.OrderId)
            .HasDatabaseName(OneOpenPaymentPerOrderIndex)
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Succeeded', 'PaidWithoutOrder')");
    }
}
