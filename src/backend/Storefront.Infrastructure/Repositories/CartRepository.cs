using System.Linq.Expressions;
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

    public async Task<int> DeleteStaleAsync(
        DateTimeOffset cutoff, bool onlyEmpty, int batchSize, CancellationToken cancellationToken)
    {
        Expression<Func<Cart, bool>> isStale = c => c.UpdatedAt < cutoff
            && (!onlyEmpty || !Context.CartItems.Any(i => i.CartId == c.Id));
        var ids = await Context.Carts.AsNoTracking()
            .Where(isStale)
            .OrderBy(c => c.UpdatedAt)
            .Select(c => c.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        // The rule is checked again in the delete: a cart changed since the ids were read is not deleted.
        return ids.Count == 0
            ? 0
            : await Context.Carts.Where(isStale).Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
