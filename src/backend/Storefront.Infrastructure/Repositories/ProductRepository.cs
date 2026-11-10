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

    public Task<bool> ExistsBySlugAsync(string slug, Guid? exceptProductId, CancellationToken cancellationToken) =>
        Query(trackChanges: false).AnyAsync(p => p.Slug == slug && p.Id != exceptProductId, cancellationToken);

    public Task<bool> ExistsBySkuAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken) =>
        Context.Set<ProductVariant>().AsNoTracking()
            .AnyAsync(v => v.Sku == sku && v.Id != exceptVariantId, cancellationToken);

    public void RemoveImage(ProductImage image) => Context.Set<ProductImage>().Remove(image);

    public void ExpectVersion(Product product, uint version) =>
        Context.Entry(product).Property(p => p.Version).OriginalValue = version;
}
