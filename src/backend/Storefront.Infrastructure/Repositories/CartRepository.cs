using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Carts;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class CartRepository(StorefrontDbContext context)
    : RepositoryBase<Cart>(context), ICartRepository
{
    public Task<Cart?> GetByTokenHashAsync(byte[] tokenHash, bool trackChanges, CancellationToken cancellationToken) =>
        Query(trackChanges)
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);

    public Task<bool> IsVariantOrderableAsync(Guid variantId, CancellationToken cancellationToken) =>
        Context.ProductVariants.AsNoTracking().AnyAsync(
            v => v.Id == variantId
                && v.IsActive
                && Context.Products.Any(p => p.Id == v.ProductId && p.Status == ProductStatus.Published),
            cancellationToken);

    public void AddItem(CartItem item) => Context.Set<CartItem>().Add(item);

    public void RemoveItem(CartItem item) => Context.Set<CartItem>().Remove(item);

    public void RemoveItems(Cart cart) => Context.Set<CartItem>().RemoveRange(cart.Items);

    public void Touch(Cart cart) => Context.Entry(cart).Property(c => c.UpdatedAt).IsModified = true;
}
