namespace Storefront.Application.Catalog;

public interface IServiceManager
{
    ICategoryService Category { get; }

    IProductService Product { get; }
}
