using Storefront.Application.Contracts;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class RepositoryManager(StorefrontDbContext context) : IRepositoryManager
{
    private readonly Lazy<ICategoryRepository> _category = new(() => new CategoryRepository(context));
    private readonly Lazy<IProductRepository> _product = new(() => new ProductRepository(context));
    private readonly Lazy<IStoreSettingsRepository> _storeSettings = new(() => new StoreSettingsRepository(context));

    public ICategoryRepository Category => _category.Value;

    public IProductRepository Product => _product.Value;

    public IStoreSettingsRepository StoreSettings => _storeSettings.Value;

    public async Task SaveAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}
