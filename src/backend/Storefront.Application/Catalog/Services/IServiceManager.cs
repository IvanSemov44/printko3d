using Storefront.Application.Carts;
using Storefront.Application.Orders;
using Storefront.Application.Payments;
using Storefront.Application.Shipping;

namespace Storefront.Application.Catalog;

public interface IServiceManager
{
    ICategoryService Category { get; }

    IProductService Product { get; }

    IStoreSettingsService StoreSettings { get; }

    ICatalogReadService Catalog { get; }

    ICartService Cart { get; }

    IOrderService Orders { get; }

    IShippingService Shipping { get; }

    IPaymentService Payments { get; }

    IShippingTariffService ShippingTariff { get; }
}
