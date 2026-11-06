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
            .Include(p => p.Images)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Query(trackChanges: false).AnyAsync(p => p.Slug == slug, cancellationToken);

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Query(trackChanges: false).AnyAsync(p => p.Variants.Any(v => v.Sku == sku), cancellationToken);
}
