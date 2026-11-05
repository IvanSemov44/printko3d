using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

public interface IProductRepository
{
    void Create(Product product);

    Task<Product?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
}
