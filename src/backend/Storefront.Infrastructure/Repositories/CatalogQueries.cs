using Microsoft.EntityFrameworkCore;
using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class CatalogQueries(StorefrontDbContext context) : ICatalogQueries
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await VisibleCategories()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.ParentId, c.Name, c.Slug, c.Description, c.ImageKey, c.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CategoryAdminDto>> GetAdminCategoriesAsync(CancellationToken cancellationToken) =>
        await AdminCategories(
                context.Categories.AsNoTracking()
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ThenBy(c => c.Id))
            .ToListAsync(cancellationToken);

    public Task<CategoryAdminDto?> GetAdminCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        AdminCategories(context.Categories.AsNoTracking().Where(c => c.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<ProductAdminListItemDto>> GetAdminProductsAsync(
        ProductAdminListQuery query,
        CancellationToken cancellationToken)
    {
        var products = context.Products.AsNoTracking();

        if (query.Status is { } status)
        {
            products = products.Where(p => p.Status == status);
        }

        if (query.CategoryId is { } categoryId)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        if (query.Search is { } search)
        {
            // % and _ typed by the admin are plain characters, not wildcards.
            var escape = ((char)92).ToString();
            var pattern = "%" + search.Replace(escape, escape + escape).Replace("%", escape + "%").Replace("_", escape + "_") + "%";
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern, escape));
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var rows = await products
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.CategoryId,
                CategoryName = context.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).First(),
                p.Status,
                p.FulfilmentType,
                VariantCount = p.Variants.Count,
                MinPrice = p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price),
                TotalStock = p.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity),
                p.Version,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ProductAdminListItemDto(
                r.Id,
                r.Name,
                r.Slug,
                r.CategoryId,
                r.CategoryName,
                r.Status,
                r.FulfilmentType,
                r.VariantCount,
                r.MinPrice,
                r.FulfilmentType == FulfilmentType.InStock ? r.TotalStock : null,
                r.Version))
            .ToList();

        return new PagedResult<ProductAdminListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ProductAdminDto?> GetAdminProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await context.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.CategoryId,
                p.Name,
                p.Slug,
                p.ShortDescription,
                p.Description,
                p.FulfilmentType,
                p.LeadTimeDays,
                p.Status,
                p.SeoTitle,
                p.SeoDescription,
                p.Version,
                Variants = p.Variants
                    .OrderBy(v => v.SortOrder)
                    .ThenBy(v => v.Id)
                    .Select(v => new
                    {
                        v.Id,
                        v.Sku,
                        v.Name,
                        v.Price,
                        v.CompareAtPrice,
                        v.StockQuantity,
                        v.WeightGrams,
                        v.Attributes,
                        v.IsActive,
                        v.SortOrder,
                    })
                    .ToList(),
                Images = p.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => new ProductImageDto(i.Id, i.VariantId, i.StorageKey, i.AltText, i.SortOrder, i.IsPrimary))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new ProductAdminDto(
                row.Id,
                row.CategoryId,
                row.Name,
                row.Slug,
                row.ShortDescription,
                row.Description,
                row.FulfilmentType,
                row.LeadTimeDays,
                row.Status,
                row.SeoTitle,
                row.SeoDescription,
                row.Version,
                row.Variants
                    .Select(v => new VariantAdminDto(
                        v.Id,
                        v.Sku,
                        v.Name,
                        v.Price,
                        v.CompareAtPrice,
                        v.StockQuantity,
                        v.WeightGrams,
                        v.Attributes,
                        v.IsActive,
                        v.SortOrder))
                    .ToList(),
                row.Images);
    }

    public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(
        ProductListQuery query,
        CancellationToken cancellationToken)
    {
        var products = VisibleProducts();

        if (query.CategorySlug is { } slug)
        {
            // The category itself or one of its direct children (the tree has at most two levels).
            var inCategory = context.Categories.AsNoTracking().Where(c => c.Slug == slug).Select(c => c.Id);
            products = products.Where(p => context.Categories.Any(c => c.Id == p.CategoryId
                && (inCategory.Contains(c.Id) || (c.ParentId != null && inCategory.Contains(c.ParentId.Value)))));
        }

        if (query.FulfilmentType is { } fulfilmentType)
        {
            products = products.Where(p => p.FulfilmentType == fulfilmentType);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var rows = products.Select(p => new
        {
            p.Id,
            p.Name,
            p.Slug,
            p.ShortDescription,
            p.FulfilmentType,
            p.LeadTimeDays,
            MinPrice = p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price) ?? 0,
            AnyInStock = p.Variants.Any(v => v.IsActive && v.StockQuantity > 0),
            ImageKey = p.Images.Where(i => i.IsPrimary).Select(i => i.StorageKey).FirstOrDefault(),
            ImageAlt = p.Images.Where(i => i.IsPrimary).Select(i => i.AltText).FirstOrDefault(),
        });

        // Every order ends with the id so that two requests never swap equal rows.
        var ordered = query.Sort switch
        {
            ProductSort.NameDescending => rows.OrderByDescending(r => r.Name).ThenBy(r => r.Id),
            ProductSort.PriceAscending => rows.OrderBy(r => r.MinPrice).ThenBy(r => r.Name).ThenBy(r => r.Id),
            ProductSort.PriceDescending => rows.OrderByDescending(r => r.MinPrice).ThenBy(r => r.Name).ThenBy(r => r.Id),
            _ => rows.OrderBy(r => r.Name).ThenBy(r => r.Id),
        };

        var skip = (query.Page - 1) * query.PageSize;
        var page = await ordered.Skip(skip).Take(query.PageSize).ToListAsync(cancellationToken);

        var items = page
            .Select(r => new ProductListItemDto(
                r.Id,
                r.Name,
                r.Slug,
                r.ShortDescription,
                r.FulfilmentType,
                r.LeadTimeDays,
                r.MinPrice,
                r.ImageKey is null ? null : new ImageRefDto(r.ImageKey, r.ImageAlt ?? string.Empty),
                ProductAvailability.IsAvailable(r.FulfilmentType, r.AnyInStock)))
            .ToList();

        return new PagedResult<ProductListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ProductDetailsDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var row = await VisibleProducts()
            .Where(p => p.Slug == slug)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.ShortDescription,
                p.Description,
                p.FulfilmentType,
                p.LeadTimeDays,
                p.SeoTitle,
                p.SeoDescription,
                Variants = p.Variants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SortOrder)
                    .ThenBy(v => v.Id)
                    .Select(v => new
                    {
                        v.Id,
                        v.Sku,
                        v.Name,
                        v.Price,
                        v.CompareAtPrice,
                        v.StockQuantity,
                        v.WeightGrams,
                        v.Attributes,
                        v.SortOrder,
                    })
                    .ToList(),
                Images = p.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => new ProductImageDto(i.Id, i.VariantId, i.StorageKey, i.AltText, i.SortOrder, i.IsPrimary))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var variants = row.Variants
            .Select(v => new ProductVariantDto(
                v.Id,
                v.Sku,
                v.Name,
                v.Price,
                v.CompareAtPrice,
                ProductAvailability.IsVariantAvailable(row.FulfilmentType, v.StockQuantity),
                v.WeightGrams,
                v.Attributes,
                v.SortOrder))
            .ToList();

        return new ProductDetailsDto(
            row.Id,
            row.Name,
            row.Slug,
            row.ShortDescription,
            row.Description,
            row.FulfilmentType,
            row.LeadTimeDays,
            row.SeoTitle ?? row.Name,
            row.SeoDescription ?? row.ShortDescription,
            variants.Any(v => v.IsAvailable),
            variants,
            row.Images);
    }

    public Task<StoreSettingsDto?> GetStoreSettingsAsync(CancellationToken cancellationToken) =>
        context.StoreSettings
            .AsNoTracking()
            .Where(s => s.Id == StoreSettings.SingletonId)
            .Select(s => new StoreSettingsDto(
                s.StoreName,
                s.DefaultCurrency,
                s.DefaultLocale,
                s.ContactEmail,
                s.ContactPhone,
                s.LogoKey,
                s.PrimaryColor,
                s.IsDemo))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SitemapEntryDto>> GetSitemapEntriesAsync(int limit, CancellationToken cancellationToken)
    {
        // Slugs are plain, but a path is built for an address, so each part is escaped.
        var categories = await VisibleCategories()
            .OrderBy(c => c.Slug)
            .Take(limit)
            .Select(c => new { c.Slug, c.UpdatedAt })
            .ToListAsync(cancellationToken);

        var products = await VisibleProducts()
            .OrderBy(p => p.Slug)
            .Take(limit - categories.Count)
            .Select(p => new { p.Slug, p.UpdatedAt })
            .ToListAsync(cancellationToken);

        return
        [
            .. categories.Select(c => new SitemapEntryDto("/categories/" + Uri.EscapeDataString(c.Slug), c.UpdatedAt)),
            .. products.Select(p => new SitemapEntryDto("/products/" + Uri.EscapeDataString(p.Slug), p.UpdatedAt)),
        ];
    }

    /// <summary>
    /// What the shop may show: published, with at least one active variant, in an active category
    /// whose parent (if any) is active too.
    /// </summary>
    private IQueryable<Product> VisibleProducts() =>
        context.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published
                && p.Variants.Any(v => v.IsActive)
                && VisibleCategories().Any(c => c.Id == p.CategoryId));

    /// <summary>Admin view of categories: no visibility rule, but with the number of products directly in each.</summary>
    private IQueryable<CategoryAdminDto> AdminCategories(IQueryable<Category> categories) =>
        categories.Select(c => new CategoryAdminDto(
            c.Id,
            c.ParentId,
            c.Name,
            c.Slug,
            c.Description,
            c.SortOrder,
            c.IsActive,
            context.Products.Count(p => p.CategoryId == c.Id),
            c.Version));

    /// <summary>Active categories whose parent (if any) is active too. The one place that rule lives.</summary>
    private IQueryable<Category> VisibleCategories() =>
        context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive
                && (c.ParentId == null
                    || context.Categories.Any(parent => parent.Id == c.ParentId && parent.IsActive)));
}
