using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Orders;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class OrderRepository(StorefrontDbContext context)
    : RepositoryBase<Order>(context), IOrderRepository
{
    public Task<bool> NumberExistsAsync(int number, CancellationToken cancellationToken) =>
        Context.Orders.AnyAsync(o => o.Number == number, cancellationToken);

    public Task<Order?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
        Query(trackChanges)
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<bool> TryMarkStockReleasedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var now = Context.Now;
        var changed = await Context.Orders
            .Where(o => o.Id == orderId && o.StockReleasedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(o => o.StockReleasedAt, now).SetProperty(o => o.UpdatedAt, now),
                cancellationToken);
        return changed == 1;
    }
}
