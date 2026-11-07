using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Contracts;

/// <summary>Read side of the storefront catalog. Returns only what shoppers may see.</summary>
public interface ICatalogQueries
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductListQuery query, CancellationToken cancellationToken);

    Task<ProductDetailsDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<StoreSettingsDto?> GetStoreSettingsAsync(CancellationToken cancellationToken);
}
