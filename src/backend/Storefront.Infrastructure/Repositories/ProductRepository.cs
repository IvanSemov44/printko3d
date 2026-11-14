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

    public Task<bool> AnyVariantsAsync(CancellationToken cancellationToken) =>
        Context.Set<ProductVariant>().AsNoTracking().AnyAsync(cancellationToken);

    public async Task<bool> TryTakeStockAsync(Guid variantId, int quantity, CancellationToken cancellationToken)
    {
        var now = Context.Now;
        var changed = await Context.Set<ProductVariant>()
            .Where(v => v.Id == variantId && v.StockQuantity >= quantity)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - quantity)
                    .SetProperty(v => v.UpdatedAt, now),
                cancellationToken);
        return changed == 1;
    }

    public async Task ReturnStockAsync(Guid variantId, int quantity, CancellationToken cancellationToken)
    {
        var now = Context.Now;
        await Context.Set<ProductVariant>()
            .Where(v => v.Id == variantId && v.StockQuantity != null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + quantity)
                    .SetProperty(v => v.UpdatedAt, now),
                cancellationToken);
    }

    public void RemoveImage(ProductImage image) => Context.Set<ProductImage>().Remove(image);

    public void ExpectVersion(Product product, uint version) =>
        Context.Entry(product).Property(p => p.Version).OriginalValue = version;
}
