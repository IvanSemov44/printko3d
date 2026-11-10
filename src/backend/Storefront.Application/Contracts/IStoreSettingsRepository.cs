using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

public interface IStoreSettingsRepository
{
    void Create(StoreSettings settings);

    Task<StoreSettings?> GetAsync(bool trackChanges, CancellationToken cancellationToken);

    /// <summary>Makes the next save fail with a ConflictException if the row changed since it was read with this version.</summary>
    void ExpectVersion(StoreSettings settings, uint version);
}
