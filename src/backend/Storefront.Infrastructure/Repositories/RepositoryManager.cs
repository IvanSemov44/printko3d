using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class RepositoryManager(StorefrontDbContext context) : IRepositoryManager
{
    private readonly Lazy<ICategoryRepository> _category = new(() => new CategoryRepository(context));
    private readonly Lazy<IProductRepository> _product = new(() => new ProductRepository(context));
    private readonly Lazy<IStoreSettingsRepository> _storeSettings = new(() => new StoreSettingsRepository(context));

    private readonly Lazy<ICatalogQueries> _catalog = new(() => new CatalogQueries(context));

    public ICatalogQueries Catalog => _catalog.Value;

    public ICategoryRepository Category => _category.Value;

    public IProductRepository Product => _product.Value;

    public IStoreSettingsRepository StoreSettings => _storeSettings.Value;

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (SaveErrorTranslator.Translate(ex) is not null)
        {
            throw SaveErrorTranslator.Translate(ex)!;
        }
    }
}
