using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

public interface IProductRepository
{
    void Create(Product product);

    Task<Product?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    /// <summary>Is the slug used by a product other than <paramref name="exceptProductId"/>?</summary>
    Task<bool> ExistsBySlugAsync(string slug, Guid? exceptProductId, CancellationToken cancellationToken);

    /// <summary>Is the SKU used by a variant other than <paramref name="exceptVariantId"/>? SKUs are unique across all products.</summary>
    Task<bool> ExistsBySkuAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken);

    /// <summary>Marks the image for deletion at the next save.</summary>
    void RemoveImage(ProductImage image);

    /// <summary>Makes the next save fail with a ConflictException if the row changed since it was read with this version.</summary>
    void ExpectVersion(Product product, uint version);
}
