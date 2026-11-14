using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class RepositoryManager(StorefrontDbContext context) : IRepositoryManager
{
    private readonly Lazy<ICategoryRepository> _category = new(() => new CategoryRepository(context));
    private readonly Lazy<IProductRepository> _product = new(() => new ProductRepository(context));
    private readonly Lazy<IStoreSettingsRepository> _storeSettings = new(() => new StoreSettingsRepository(context));

    private readonly Lazy<ICartRepository> _cart = new(() => new CartRepository(context));
    private readonly Lazy<ICartQueries> _cartQueries = new(() => new CartQueries(context));

    private readonly Lazy<IOrderRepository> _order = new(() => new OrderRepository(context));

    private readonly Lazy<ICatalogQueries> _catalog = new(() => new CatalogQueries(context));

    public ICatalogQueries Catalog => _catalog.Value;

    public ICategoryRepository Category => _category.Value;

    public IProductRepository Product => _product.Value;

    public IStoreSettingsRepository StoreSettings => _storeSettings.Value;

    public ICartRepository Cart => _cart.Value;

    public ICartQueries CartQueries => _cartQueries.Value;

    public IOrderRepository Order => _order.Value;

    public async Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new RepositoryTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

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
