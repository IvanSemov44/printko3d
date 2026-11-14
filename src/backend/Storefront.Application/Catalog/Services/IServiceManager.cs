using Storefront.Application.Carts;
using Storefront.Application.Orders;
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

    IShippingTariffService ShippingTariff { get; }
}
