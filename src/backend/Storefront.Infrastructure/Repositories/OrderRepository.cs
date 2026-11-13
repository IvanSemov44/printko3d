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
}
