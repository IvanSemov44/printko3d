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
}
