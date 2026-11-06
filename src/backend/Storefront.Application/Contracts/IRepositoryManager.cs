namespace Storefront.Application.Contracts;

public interface IRepositoryManager
{
    ICategoryRepository Category { get; }

    IProductRepository Product { get; }

    IStoreSettingsRepository StoreSettings { get; }

    Task SaveAsync(CancellationToken cancellationToken);
}
