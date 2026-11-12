using Storefront.Application.Carts;

namespace Storefront.Application.Catalog;

public interface IServiceManager
{
    ICategoryService Category { get; }

    IProductService Product { get; }

    IStoreSettingsService StoreSettings { get; }

    ICatalogReadService Catalog { get; }

    ICartService Cart { get; }
}
