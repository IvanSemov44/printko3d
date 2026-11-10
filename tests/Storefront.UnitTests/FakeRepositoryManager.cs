using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

internal sealed class FakeRepositoryManager : IRepositoryManager
{
    private readonly FakeCategoryRepository _category = new();
    private readonly FakeProductRepository _product = new();
    private readonly FakeStoreSettingsRepository _storeSettings = new();

    public List<Category> Categories => _category.Items;

    public List<Product> Products => _product.Items;

    public int SaveCount { get; private set; }

    public ICategoryRepository Category => _category;

    public IProductRepository Product => _product;

    public IStoreSettingsRepository StoreSettings => _storeSettings;

    public ICatalogQueries Catalog => throw new NotSupportedException("Read queries are covered by the integration tests.");

    public List<StoreSettings> Settings => _storeSettings.Items;

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        // Like the real save: a stale version is a conflict and nothing is written.
        if (_category.Expected.Any(e => e.Key.Version != e.Value))
        {
            _category.Expected.Clear();
            throw new ConflictException("The record was changed by someone else.", "category.version_conflict");
        }

        if (_product.Expected.Any(e => e.Key.Version != e.Value))
        {
            _product.Expected.Clear();
            throw new ConflictException("The record was changed by someone else.", "product.version_conflict");
        }

        _category.Expected.Clear();
        _product.Expected.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Items { get; } = [];

        public void Create(Category category) => Items.Add(category);

        public Task<Category?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(c => c.Id == id));

        public Task<Category?> GetBySlugAsync(string slug, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(c => c.Slug == slug));

        public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(c => c.ParentId == id));

        public Task<IReadOnlyList<Category>> GetSiblingsAsync(Guid? parentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>(Items
                .Where(c => c.ParentId == parentId)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .ToList());

        // The fake keeps the version the caller expects and compares it with the stored one when saving.
        public Dictionary<Category, uint> Expected { get; } = [];

        public void ExpectVersion(Category category, uint version) => Expected[category] = version;
    }

    private sealed class FakeStoreSettingsRepository : IStoreSettingsRepository
    {
        public List<StoreSettings> Items { get; } = [];

        public void Create(StoreSettings settings) => Items.Add(settings);

        public Task<StoreSettings?> GetAsync(bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault());
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Items { get; } = [];

        public void Create(Product product) => Items.Add(product);

        public Task<Product?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(p => p.Id == id));

        public Dictionary<Product, uint> Expected { get; } = [];

        public void ExpectVersion(Product product, uint version) => Expected[product] = version;

        public Task<bool> ExistsBySlugAsync(string slug, Guid? exceptProductId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(p => p.Slug == slug && p.Id != exceptProductId));

        public Task<bool> ExistsBySkuAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(p => p.Variants.Any(v => v.Sku == sku && v.Id != exceptVariantId)));
    }
}
