using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Payments;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("payment_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Provider).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EventId).HasMaxLength(200).IsRequired();

        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(e => e.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.PaymentId);

        // The second message with the same number is the repeat the provider is allowed to send.
        builder.HasIndex(e => new { e.Provider, e.EventId }).IsUnique();
    }
}
