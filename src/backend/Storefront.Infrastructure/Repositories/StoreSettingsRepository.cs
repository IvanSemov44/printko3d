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

    public void ExpectVersion(StoreSettings settings, uint version) =>
        Context.Entry(settings).Property(s => s.Version).OriginalValue = version;
}
