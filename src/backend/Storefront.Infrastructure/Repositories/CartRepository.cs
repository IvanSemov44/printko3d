using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Carts;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class CartRepository(StorefrontDbContext context)
    : RepositoryBase<Cart>(context), ICartRepository
{
    public Task<Cart?> GetByTokenHashAsync(byte[] tokenHash, bool trackChanges, CancellationToken cancellationToken) =>
        Query(trackChanges)
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);

    public void RemoveItem(CartItem item) => Context.Set<CartItem>().Remove(item);

    public void RemoveItems(Cart cart) => Context.Set<CartItem>().RemoveRange(cart.Items);
}
