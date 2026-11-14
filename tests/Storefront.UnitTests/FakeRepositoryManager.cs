using Storefront.Application.Carts.Queries;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Carts;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

internal sealed class FakeRepositoryManager : IRepositoryManager
{
    private readonly FakeCategoryRepository _category = new();
    private readonly FakeProductRepository _product = new();
    private readonly FakeStoreSettingsRepository _storeSettings = new();
    private readonly FakeCartRepository _cart = new();
    private readonly FakeCartQueries _cartQueries;

    public List<Category> Categories => _category.Items;

    public List<Product> Products => _product.Items;

    public int SaveCount { get; private set; }

    public ICategoryRepository Category => _category;

    public IProductRepository Product => _product;

    public IStoreSettingsRepository StoreSettings => _storeSettings;

    public ICartRepository Cart => _cart;

    public List<Cart> Carts => _cart.Items;

    public HashSet<Guid> OrderableVariants => _cart.OrderableVariants;

    public Dictionary<Guid, int> StockLimits => _cart.StockLimits;

    public List<Cart> TouchedCarts => _cart.Touched;

    public ICartQueries CartQueries => _cartQueries;

    public int CartDeleteCalls => _cart.DeleteCalls;

    public ICatalogQueries Catalog => throw new NotSupportedException("Read queries are covered by the integration tests.");

    public List<StoreSettings> Settings => _storeSettings.Items;

    /// <summary>When set, the next saves fail with this exception, like a database that is down.</summary>
    public Exception? SaveFailure { get; set; }

    // Orders are tested against a real database (OrderServiceDatabaseTests); no unit test places one.
    public IOrderRepository Order => throw new NotSupportedException("The unit tests do not place orders.");

    public Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Transactions are covered by the database tests.");

    public FakeRepositoryManager() => _cartQueries = new FakeCartQueries(_cart);


    public Task SaveAsync(CancellationToken cancellationToken)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

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

        if (_storeSettings.Expected.Any(e => e.Key.Version != e.Value))
        {
            _storeSettings.Expected.Clear();
            throw new ConflictException("The record was changed by someone else.", "store_settings.version_conflict");
        }

        _category.Expected.Clear();
        _product.Expected.Clear();
        _storeSettings.Expected.Clear();
        foreach (var settings in _storeSettings.Items)
        {
            // Like xmin, the version changes on every write.
            settings.Version++;
        }

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

        public Dictionary<StoreSettings, uint> Expected { get; } = [];

        public void ExpectVersion(StoreSettings settings, uint version) => Expected[settings] = version;
    }

    private sealed class FakeCartRepository : ICartRepository
    {
        public List<Cart> Items { get; } = [];

        public HashSet<Guid> OrderableVariants { get; } = [];

        public List<Cart> Touched { get; } = [];

        public void Create(Cart cart) => Items.Add(cart);

        public Task<Cart?> GetByTokenHashAsync(byte[] tokenHash, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(c => c.TokenHash.SequenceEqual(tokenHash)));

        // A variant in StockLimits is in stock with that many units; any other orderable variant is made to order.
        public Dictionary<Guid, int> StockLimits { get; } = [];

        public Task<VariantSupply?> GetSupplyAsync(Guid variantId, CancellationToken cancellationToken) =>
            Task.FromResult<VariantSupply?>(StockLimits.TryGetValue(variantId, out var stock)
                ? new VariantSupply(OrderableVariants.Contains(variantId), FulfilmentType.InStock, stock)
                : new VariantSupply(OrderableVariants.Contains(variantId), FulfilmentType.MadeToOrder, null));

        // The service also adds the line to the cart, like the change tracker does with the real database.
        public void AddItem(CartItem item)
        {
        }

        public void RemoveItem(CartItem item) => Items.ForEach(c => c.Items.Remove(item));

        public void RemoveItems(Cart cart) => cart.Items.Clear();

        public void Touch(Cart cart) => Touched.Add(cart);

        public int DeleteCalls { get; private set; }

        public Task<int> DeleteStaleAsync(
            DateTimeOffset cutoff, bool onlyEmpty, int batchSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteCalls++;
            var stale = Items
                .Where(c => c.UpdatedAt < cutoff && (!onlyEmpty || c.Items.Count == 0))
                .OrderBy(c => c.UpdatedAt)
                .Take(batchSize)
                .ToList();
            Items.RemoveAll(stale.Contains);
            return Task.FromResult(stale.Count);
        }
    }

    private sealed class FakeCartQueries(FakeCartRepository carts) : ICartQueries
    {
        // Prices, names and states are covered by the integration tests; here only the lines and the counts matter.
        public Task<CartDto> GetAsync(byte[] tokenHash, CancellationToken cancellationToken)
        {
            var cart = carts.Items.SingleOrDefault(c => c.TokenHash.SequenceEqual(tokenHash));
            var lines = (cart?.Items ?? []).Select(i => new CartLineDto(
                i.VariantId, "Product", "product", "Variant", "SKU", null, 100, i.Quantity, 100 * i.Quantity,
                CartLineState.Available, FulfilmentType.InStock, null, null)).ToList();
            return Task.FromResult(CartDto.From(lines));
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Items { get; } = [];

        public void Create(Product product) => Items.Add(product);

        public Task<Product?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(p => p.Id == id));

        public Task<bool> TryTakeStockAsync(Guid variantId, int quantity, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Stock is covered by the database tests.");

        public Task ReturnStockAsync(Guid variantId, int quantity, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Stock is covered by the database tests.");

        public void RemoveImage(ProductImage image)
        {
            // The real save deletes the row; here it goes at once, which is the same for the service.
            foreach (var product in Items)
            {
                product.Images.Remove(image);
            }
        }

        public Task<bool> AnyVariantsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(p => p.Variants.Count > 0));

        public Dictionary<Product, uint> Expected { get; } = [];

        public void ExpectVersion(Product product, uint version) => Expected[product] = version;

        public Task<bool> ExistsBySlugAsync(string slug, Guid? exceptProductId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(p => p.Slug == slug && p.Id != exceptProductId));

        public Task<bool> ExistsBySkuAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(p => p.Variants.Any(v => v.Sku == sku && v.Id != exceptVariantId)));
    }
}
