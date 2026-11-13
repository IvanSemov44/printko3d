using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

/// <summary>The product lookup that the product, variant and image services share.</summary>
internal static class TrackedProducts
{
    public static async Task<Product> GetAsync(
        IRepositoryManager repositories,
        Guid productId,
        CancellationToken cancellationToken) =>
        await repositories.Product.GetByIdAsync(productId, trackChanges: true, cancellationToken)
            ?? throw NotFound(productId);

    public static NotFoundException NotFound(Guid id) =>
        new($"Product {id} was not found.", "product.not_found");
}
