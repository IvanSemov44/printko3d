using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Infrastructure.Identity;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class CustomerUserConfiguration : IEntityTypeConfiguration<CustomerUser>
{
    public void Configure(EntityTypeBuilder<CustomerUser> builder)
    {
        builder.ToTable("customer_users");
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(254);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(254);
        builder.Property(u => u.UserName).HasMaxLength(254);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(254);
        builder.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
        builder.HasIndex(u => u.NormalizedUserName).IsUnique();
    }
}

internal sealed class CustomerExternalLoginConfiguration : IEntityTypeConfiguration<CustomerExternalLogin>
{
    public void Configure(EntityTypeBuilder<CustomerExternalLogin> builder)
    {
        builder.ToTable("customer_external_logins");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Provider).HasMaxLength(50).IsRequired();
        builder.Property(l => l.ProviderKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(l => new { l.Provider, l.ProviderKey }).IsUnique();
        builder.HasIndex(l => l.CustomerId);
        builder.HasOne<CustomerUser>().WithMany().HasForeignKey(l => l.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}
