using Storefront.Application.Contracts;

namespace Storefront.Application.Catalog;

public sealed class ServiceManager(IRepositoryManager repositories) : IServiceManager
{
    private readonly Lazy<ICategoryService> _category = new(() => new CategoryService(repositories));
    private readonly Lazy<IProductService> _product = new(() => new ProductService(repositories));

    public ICategoryService Category => _category.Value;

    public IProductService Product => _product.Value;
}
