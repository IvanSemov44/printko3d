using Microsoft.EntityFrameworkCore;
using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

/// <summary>The admin panel's reads of products: the list and one product with its variants and images.</summary>
internal sealed class AdminProductQueries(StorefrontDbContext context)
{
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
}
