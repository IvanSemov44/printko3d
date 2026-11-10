using Storefront.Application.Catalog.Queries;

namespace Storefront.Application.Contracts;

/// <summary>Read side of the storefront catalog. Returns only what shoppers may see.</summary>
public interface ICatalogQueries
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductListQuery query, CancellationToken cancellationToken);

    Task<ProductDetailsDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Admin side: every category, the inactive ones too, with product counts.</summary>
    Task<IReadOnlyList<CategoryAdminDto>> GetAdminCategoriesAsync(CancellationToken cancellationToken);

    Task<CategoryAdminDto?> GetAdminCategoryAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Admin side: every product whatever its status, one page at a time.</summary>
    Task<PagedResult<ProductAdminListItemDto>> GetAdminProductsAsync(ProductAdminListQuery query, CancellationToken cancellationToken);

    /// <summary>Admin side: one product with all its variants, the inactive ones too.</summary>
    Task<ProductAdminDto?> GetAdminProductAsync(Guid id, CancellationToken cancellationToken);

    Task<StoreSettingsDto?> GetStoreSettingsAsync(CancellationToken cancellationToken);
}
