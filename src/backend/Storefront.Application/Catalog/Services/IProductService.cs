using Storefront.Application.Catalog.Queries;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

public interface IProductService
{
    Task<Guid> CreateAsync(NewProduct product, CancellationToken cancellationToken);

    Task UpdateAsync(Guid productId, UpdateProduct input, CancellationToken cancellationToken);

    /// <summary>Publishing needs an active variant. Archiving and going back to draft hide the product from the shop.</summary>
    Task SetStatusAsync(Guid productId, ProductStatus status, uint version, CancellationToken cancellationToken);

    Task<Guid> AddVariantAsync(Guid productId, NewVariant variant, CancellationToken cancellationToken);

    Task UpdateVariantAsync(Guid productId, Guid variantId, UpdateVariant input, CancellationToken cancellationToken);

    /// <summary>The last active variant of a published product cannot be deactivated.</summary>
    Task SetVariantActiveAsync(Guid productId, Guid variantId, bool isActive, CancellationToken cancellationToken);

    Task MoveVariantAsync(Guid productId, Guid variantId, MoveDirection direction, CancellationToken cancellationToken);

    Task<Guid> AddImageAsync(Guid productId, NewImage image, CancellationToken cancellationToken);

    Task PublishAsync(Guid productId, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid productId, CancellationToken cancellationToken);

    Task<PagedResult<ProductAdminListItemDto>> GetListAsync(ProductAdminListQuery query, CancellationToken cancellationToken);

    Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken);
}
