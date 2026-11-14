using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Domain.Carts;
using Storefront.Infrastructure.Identity;

namespace Storefront.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts", table => table.HasCheckConstraint(
            "ck_carts_token_hash_length",
            "octet_length(token_hash) = 32"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.TokenHash).IsRequired();
        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => c.UpdatedAt);
        builder.HasIndex(c => c.CustomerId).IsUnique().HasFilter("customer_id IS NOT NULL");
        builder.HasOne<CustomerUser>().WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
