namespace Storefront.Application.Contracts;

public interface IRepositoryManager
{
    ICategoryRepository Category { get; }

    IProductRepository Product { get; }

    IStoreSettingsRepository StoreSettings { get; }

    ICartRepository Cart { get; }

    ICartQueries CartQueries { get; }

    IOrderRepository Order { get; }

    ICatalogQueries Catalog { get; }

    Task SaveAsync(CancellationToken cancellationToken);
}
