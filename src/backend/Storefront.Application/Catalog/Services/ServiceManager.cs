using Storefront.Application.Carts;
using Storefront.Application.Contracts;
using Storefront.Application.Orders;

namespace Storefront.Application.Catalog;

public sealed class ServiceManager(
    IRepositoryManager repositories,
    IImageStorage imageStorage,
    IOrderNumberGenerator? orderNumbers = null,
    TimeProvider? timeProvider = null,
    OrderOptions? orderOptions = null) : IServiceManager
{
    private readonly Lazy<ICategoryService> _category = new(() => new CategoryService(repositories));
    private readonly Lazy<IProductService> _product = new(() => new ProductService(repositories, imageStorage));

    private readonly Lazy<IStoreSettingsService> _storeSettings = new(() => new StoreSettingsService(repositories));
    private readonly Lazy<ICatalogReadService> _catalog = new(() => new CatalogReadService(repositories));
    private readonly Lazy<ICartService> _cart = new(() => new CartService(repositories));

    // The order settings are optional so that code which only needs the catalog does not have to build them;
    // the API registers the real ones.
    private readonly Lazy<IOrderService> _orders = new(() => new OrderService(
        repositories,
        orderNumbers ?? new RandomOrderNumberGenerator(),
        timeProvider ?? TimeProvider.System,
        orderOptions ?? new OrderOptions()));

    public ICategoryService Category => _category.Value;

    public IProductService Product => _product.Value;

    public IStoreSettingsService StoreSettings => _storeSettings.Value;

    public ICatalogReadService Catalog => _catalog.Value;

    public ICartService Cart => _cart.Value;

    public IOrderService Orders => _orders.Value;
}
