using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

public interface IStoreSettingsRepository
{
    void Create(StoreSettings settings);

    Task<StoreSettings?> GetAsync(bool trackChanges, CancellationToken cancellationToken);
}
