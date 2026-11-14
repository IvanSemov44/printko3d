using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Emails;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class OutgoingEmailConfiguration : IEntityTypeConfiguration<OutgoingEmail>
{
    public void Configure(EntityTypeBuilder<OutgoingEmail> builder)
    {
        builder.ToTable("outgoing_emails", table =>
        {
            table.HasCheckConstraint("ck_outgoing_emails_status", "status IN ('Pending', 'Sent', 'Failed')");
            table.HasCheckConstraint("ck_outgoing_emails_attempts", "attempts >= 0");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ToAddress).HasMaxLength(254).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TextBody).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        // The sender reads the pending messages whose time has come.
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });
    }
}
