using Storefront.Application.Contracts;
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

        public Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(p => p.Slug == slug));

        public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(p => p.Variants.Any(v => v.Sku == sku)));
    }
}
