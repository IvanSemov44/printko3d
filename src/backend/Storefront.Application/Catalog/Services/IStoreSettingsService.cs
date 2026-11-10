namespace Storefront.Application.Catalog;

public interface IStoreSettingsService
{
    /// <summary>Creates the single settings row if it is missing. Returns false when a row already exists.</summary>
    Task<bool> EnsureAsync(NewStoreSettings settings, CancellationToken cancellationToken);
}
