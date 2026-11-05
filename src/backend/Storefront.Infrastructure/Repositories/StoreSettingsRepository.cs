using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class StoreSettingsRepository(StorefrontDbContext context)
    : RepositoryBase<StoreSettings>(context), IStoreSettingsRepository
{
    public Task<StoreSettings?> GetAsync(bool trackChanges, CancellationToken cancellationToken) =>
        Query(trackChanges).SingleOrDefaultAsync(cancellationToken);
}
