using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class ProductRepository(StorefrontDbContext context)
    : RepositoryBase<Product>(context), IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
        Query(trackChanges)
            .Include(p => p.Variants)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
}
