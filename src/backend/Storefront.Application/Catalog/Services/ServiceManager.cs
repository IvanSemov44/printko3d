using Storefront.Application.Carts;
using Storefront.Application.Contracts;
using Storefront.Application.Orders;
using Storefront.Application.Payments;
using Storefront.Application.Shipping;

namespace Storefront.Application.Catalog;

public sealed class ServiceManager(
    IRepositoryManager repositories,
    IImageStorage imageStorage,
    IOrderNumberGenerator? orderNumbers = null,
    TimeProvider? timeProvider = null,
    OrderOptions? orderOptions = null,
    IShippingProvider? shippingProvider = null,
    PaymentOptions? paymentOptions = null,
    IPaymentProvider? paymentProvider = null,
    IPaymentSimulator? paymentSimulator = null) : IServiceManager
{
    private readonly Lazy<ICategoryService> _category = new(() => new CategoryService(repositories));
    private readonly Lazy<IProductService> _product = new(() => new ProductService(repositories, imageStorage));

    private readonly Lazy<IStoreSettingsService> _storeSettings = new(() => new StoreSettingsService(repositories));
    private readonly Lazy<ICatalogReadService> _catalog = new(() => new CatalogReadService(repositories));
    private readonly Lazy<ICartService> _cart = new(() => new CartService(repositories));

    // The order and shipping settings are optional so that code which only needs the catalog does not have to build
    // them; the API registers the real ones. Without a provider no delivery method is offered.
    private readonly Lazy<IOrderService> _orders = new(() => new OrderService(
        repositories,
        orderNumbers ?? new RandomOrderNumberGenerator(),
        timeProvider ?? TimeProvider.System,
        orderOptions ?? new OrderOptions(),
        shippingProvider ?? new NoShippingProvider(),
        paymentOptions ?? new PaymentOptions()));

    // Without a provider the service cannot be built; the API always registers the real ones.
    private readonly Lazy<IPaymentService> _payments = new(() => new PaymentService(
        repositories,
        paymentProvider ?? throw new InvalidOperationException("No payment provider is registered."),
        paymentSimulator ?? throw new InvalidOperationException("No payment simulator is registered."),
        timeProvider ?? TimeProvider.System,
        paymentOptions ?? new PaymentOptions(),
        orderOptions ?? new OrderOptions()));

    private readonly Lazy<IOrderStatusService> _orderStatuses = new(() => new OrderStatusService(repositories, orderOptions ?? new OrderOptions()));

    private readonly Lazy<IShippingService> _shipping = new(() => new ShippingService(
        repositories,
        shippingProvider ?? new NoShippingProvider()));

    private readonly Lazy<IShippingTariffService> _shippingTariff = new(() => new ShippingTariffService(repositories));

    public ICategoryService Category => _category.Value;

    public IProductService Product => _product.Value;

    public IStoreSettingsService StoreSettings => _storeSettings.Value;

    public ICatalogReadService Catalog => _catalog.Value;

    public ICartService Cart => _cart.Value;

    public IOrderService Orders => _orders.Value;

    public IShippingService Shipping => _shipping.Value;

    public IPaymentService Payments => _payments.Value;

    public IShippingTariffService ShippingTariff => _shippingTariff.Value;

    public IOrderStatusService OrderStatuses => _orderStatuses.Value;
}
