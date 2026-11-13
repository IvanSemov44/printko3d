using Microsoft.EntityFrameworkCore;
using Storefront.Application.Catalog.Queries;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

/// <summary>The admin panel's reads of categories: no visibility rule.</summary>
internal sealed class AdminCategoryQueries(StorefrontDbContext context)
{
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
}
