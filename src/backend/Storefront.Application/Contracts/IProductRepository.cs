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

    /// <summary>Is there at least one variant in the shop? Prices are stored as whole numbers in the currency of the shop.</summary>
    Task<bool> AnyVariantsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Takes <paramref name="quantity"/> from the stock of the variant in one conditional statement
    /// (<c>WHERE stock_quantity >= quantity</c>), so two buyers cannot take the same last piece. Returns false, and changes
    /// nothing, when there is less than that or the variant has no stock figure. It does not wait for a save.
    /// </summary>
    Task<bool> TryTakeStockAsync(Guid variantId, int quantity, CancellationToken cancellationToken);

    /// <summary>Gives <paramref name="quantity"/> back. A variant without a stock figure (made to order) is left as it is.</summary>
    Task ReturnStockAsync(Guid variantId, int quantity, CancellationToken cancellationToken);

    /// <summary>Marks the image for deletion at the next save.</summary>
    void RemoveImage(ProductImage image);

    /// <summary>Makes the next save fail with a ConflictException if the row changed since it was read with this version.</summary>
    void ExpectVersion(Product product, uint version);
}
