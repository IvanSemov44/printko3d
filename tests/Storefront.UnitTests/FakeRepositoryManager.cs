using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

internal sealed class FakeRepositoryManager : IRepositoryManager
{
    private readonly FakeCategoryRepository _category = new();
    private readonly FakeProductRepository _product = new();

    public List<Category> Categories => _category.Items;

    public List<Product> Products => _product.Items;

    public int SaveCount { get; private set; }

    public ICategoryRepository Category => _category;

    public IProductRepository Product => _product;

    public IStoreSettingsRepository StoreSettings => throw new NotSupportedException();

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
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Items { get; } = [];

        public void Create(Product product) => Items.Add(product);

        public Task<Product?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(p => p.Id == id));
    }
}
