using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Shipping;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class ShippingRateRepository(StorefrontDbContext context)
    : RepositoryBase<ShippingRate>(context), IShippingRateRepository
{
    public async Task<IReadOnlyList<ShippingRate>> ListAsync(string methodCode, CancellationToken cancellationToken) =>
        await Query(trackChanges: false)
            .Where(r => r.MethodCode == methodCode)
            .OrderBy(r => r.MaxWeightGrams)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(string methodCode, CancellationToken cancellationToken) =>
        Query(trackChanges: false).AnyAsync(r => r.MethodCode == methodCode, cancellationToken);
}
