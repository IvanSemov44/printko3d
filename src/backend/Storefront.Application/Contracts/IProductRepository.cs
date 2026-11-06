using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

public interface IProductRepository
{
    void Create(Product product);

    Task<Product?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken);
}
