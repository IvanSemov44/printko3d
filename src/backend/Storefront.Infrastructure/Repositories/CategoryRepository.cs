using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class CategoryRepository(StorefrontDbContext context)
    : RepositoryBase<Category>(context), ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
        Query(trackChanges).SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Category?> GetBySlugAsync(string slug, bool trackChanges, CancellationToken cancellationToken) =>
        Query(trackChanges).SingleOrDefaultAsync(c => c.Slug == slug, cancellationToken);

    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken) =>
        Context.Categories.AsNoTracking().AnyAsync(c => c.ParentId == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetSiblingsAsync(Guid? parentId, CancellationToken cancellationToken) =>
        await Context.Categories
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public void ExpectVersion(Category category, uint version) =>
        Context.Entry(category).Property(c => c.Version).OriginalValue = version;
}
