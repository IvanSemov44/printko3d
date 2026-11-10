using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Catalog;

public interface ICatalogReadService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<PagedResult<ProductListItemDto>> GetProductsAsync(
        string? category,
        string? fulfilmentType,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Throws NotFoundException when the product is missing or not visible in the shop.</summary>
    Task<ProductDetailsDto> GetProductAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Throws NotFoundException when the settings row is missing.</summary>
    Task<StoreSettingsDto> GetStoreSettingsAsync(CancellationToken cancellationToken);
}
