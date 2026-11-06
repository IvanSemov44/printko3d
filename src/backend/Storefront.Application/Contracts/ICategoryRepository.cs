using Storefront.Domain.Catalog;

namespace Storefront.Application.Contracts;

public interface ICategoryRepository
{
    void Create(Category category);

    Task<Category?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    Task<Category?> GetBySlugAsync(string slug, bool trackChanges, CancellationToken cancellationToken);
}
