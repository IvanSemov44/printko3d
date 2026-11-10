using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;

namespace Storefront.Application.Catalog;

internal sealed class CatalogReadService(IRepositoryManager repositories) : ICatalogReadService
{
    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        repositories.Catalog.GetCategoriesAsync(cancellationToken);

    public Task<PagedResult<ProductListItemDto>> GetProductsAsync(
        string? category,
        string? fulfilmentType,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ProductListQuery.Create(category, fulfilmentType, sort, page, pageSize);
        return repositories.Catalog.GetProductsAsync(query, cancellationToken);
    }

    public async Task<ProductDetailsDto> GetProductAsync(string slug, CancellationToken cancellationToken) =>
        await repositories.Catalog.GetProductBySlugAsync(slug, cancellationToken)
            ?? throw new NotFoundException("The product was not found.", "product.not_found");

    public async Task<StoreSettingsDto> GetStoreSettingsAsync(CancellationToken cancellationToken) =>
        await repositories.Catalog.GetStoreSettingsAsync(cancellationToken)
            ?? throw new NotFoundException("Store settings were not found.", "store_settings.not_found");

    public async Task<string> GetSitemapAsync(string siteUrl, CancellationToken cancellationToken) =>
        SitemapBuilder.Build(
            siteUrl,
            await repositories.Catalog.GetSitemapEntriesAsync(SitemapBuilder.MaxEntries, cancellationToken));
}
