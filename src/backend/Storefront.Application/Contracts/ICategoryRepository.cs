using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

public interface ICategoryRepository
{
    void Create(Category category);

    Task<Category?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    Task<Category?> GetBySlugAsync(string slug, bool trackChanges, CancellationToken cancellationToken);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The categories with the same parent, ordered by sortOrder, name and id. Tracked, so the order can be rewritten.</summary>
    Task<IReadOnlyList<Category>> GetSiblingsAsync(Guid? parentId, CancellationToken cancellationToken);

    /// <summary>Makes the next save fail with a ConflictException if the row changed since it was read with this version.</summary>
    void ExpectVersion(Category category, uint version);
}
