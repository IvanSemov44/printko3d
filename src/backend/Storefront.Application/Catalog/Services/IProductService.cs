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

    /// <summary>
    /// Checks the file (type from its first bytes, size, count), stores it and adds the row. The first image of a product
    /// is its primary one. Returns the id of the new image.
    /// </summary>
    Task<Guid> UploadImageAsync(Guid productId, NewImageUpload input, CancellationToken cancellationToken);

    Task UpdateImageAsync(Guid productId, Guid imageId, UpdateImage input, CancellationToken cancellationToken);

    /// <summary>Makes the image the primary one of its product and the old primary one an ordinary image.</summary>
    Task SetPrimaryImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken);

    /// <summary>Moves an image one place and renumbers the images of the product 0, 1, 2...</summary>
    Task MoveImageAsync(Guid productId, Guid imageId, MoveDirection direction, CancellationToken cancellationToken);

    /// <summary>Removes the row and the file. If the primary image goes, the first one in order becomes primary.</summary>
    Task DeleteImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken);

    Task PublishAsync(Guid productId, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid productId, CancellationToken cancellationToken);

    Task<PagedResult<ProductAdminListItemDto>> GetListAsync(ProductAdminListQuery query, CancellationToken cancellationToken);

    Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken);
}
