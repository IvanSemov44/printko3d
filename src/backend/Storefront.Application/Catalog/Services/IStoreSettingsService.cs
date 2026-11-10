using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Catalog;

public interface IStoreSettingsService
{
    /// <summary>Creates the single settings row if it is missing. Returns false when a row already exists.</summary>
    Task<bool> EnsureAsync(NewStoreSettings settings, CancellationToken cancellationToken);

    /// <summary>The settings with the row version. Throws NotFoundException (store_settings.not_found) when the row is missing.</summary>
    Task<StoreSettingsAdminDto> GetAdminAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the editable fields. Throws a ConflictException when the version is old (store_settings.version_conflict)
    /// or the currency may not change (store_settings.currency_locked).
    /// </summary>
    Task<StoreSettingsAdminDto> UpdateAsync(UpdateStoreSettings settings, CancellationToken cancellationToken);
}
