using Storefront.Application.Carts;
using Storefront.Application.Contracts;

namespace Storefront.Application.Catalog;

public sealed class ServiceManager(IRepositoryManager repositories, IImageStorage imageStorage) : IServiceManager
{
    private readonly Lazy<ICategoryService> _category = new(() => new CategoryService(repositories));
    private readonly Lazy<IProductService> _product = new(() => new ProductService(repositories, imageStorage));

    private readonly Lazy<IStoreSettingsService> _storeSettings = new(() => new StoreSettingsService(repositories));
    private readonly Lazy<ICatalogReadService> _catalog = new(() => new CatalogReadService(repositories));
    private readonly Lazy<ICartService> _cart = new(() => new CartService(repositories));

    public ICategoryService Category => _category.Value;

    public IProductService Product => _product.Value;

    public IStoreSettingsService StoreSettings => _storeSettings.Value;

    public ICatalogReadService Catalog => _catalog.Value;

    public ICartService Cart => _cart.Value;
}
