using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class RepositoryManager(StorefrontDbContext context) : IRepositoryManager
{
    private const string UniqueViolation = "23505";
    private const string CategorySlugIndex = "IX_categories_slug";

    private readonly Lazy<ICategoryRepository> _category = new(() => new CategoryRepository(context));
    private readonly Lazy<IProductRepository> _product = new(() => new ProductRepository(context));
    private readonly Lazy<IStoreSettingsRepository> _storeSettings = new(() => new StoreSettingsRepository(context));

    private readonly Lazy<ICatalogQueries> _catalog = new(() => new CatalogQueries(context));

    public ICatalogQueries Catalog => _catalog.Value;

    public ICategoryRepository Category => _category.Value;

    public IProductRepository Product => _product.Value;

    public IStoreSettingsRepository StoreSettings => _storeSettings.Value;

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The record was changed by someone else. Reload it and try again.",
                "category.version_conflict");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: UniqueViolation,
            ConstraintName: CategorySlugIndex,
        })
        {
            throw new ConflictException("The slug is already used.", "category.slug_taken");
        }
    }
}
