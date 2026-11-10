using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Catalog;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> builder)
    {
        builder.ToTable("store_settings", table => table.HasCheckConstraint(
            "ck_store_settings_single_row",
            $"id = '{StoreSettings.SingletonId}'"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.StoreName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.DefaultCurrency).HasMaxLength(3).IsRequired();
        builder.Property(s => s.DefaultLocale).HasMaxLength(10).IsRequired();
        builder.Property(s => s.ContactEmail).HasMaxLength(320).IsRequired();
        builder.Property(s => s.ContactPhone).HasMaxLength(50).IsRequired();
        builder.Property(s => s.LogoKey).HasMaxLength(500);
        builder.Property(s => s.PrimaryColor).HasMaxLength(20).IsRequired();

        builder.Property(s => s.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
