using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Domain.Catalog;
using Storefront.Infrastructure.Persistence;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.ReadApi;

/// <summary>A fresh database plus an API over it, with a counter of the SQL commands the API runs.</summary>
internal sealed class ReadApiHost : IAsyncDisposable
{
    private readonly SqlCommandCounter _counter = new();
    private readonly WebApplicationFactory<Program> _factory;

    private ReadApiHost(TestDatabase database)
    {
        Database = database;
        _factory = new ApiFactory(database.ConnectionString).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.ConfigureDbContext<StorefrontDbContext>(options => options.AddInterceptors(_counter))));
        Client = _factory.CreateClient();
    }

    public TestDatabase Database { get; }

    public HttpClient Client { get; }

    public int SqlCommandCount => _counter.Count;

    public static async Task<ReadApiHost> CreateAsync(PostgresFixture postgres) =>
        new(await postgres.CreateDatabaseAsync());

    public async Task SeedAsync(Action<StorefrontDbContext> add)
    {
        await using var context = Database.CreateContext();
        add(context);
        await context.SaveChangesAsync();
    }

    public async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Runs the request and returns how many SQL commands it needed.</summary>
    public async Task<int> CountSqlAsync(string url)
    {
        var before = _counter.Count;
        (await Client.GetAsync(url)).EnsureSuccessStatusCode();
        return _counter.Count - before;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

/// <summary>Builds catalog rows for read tests. Products are published and in stock unless a test says otherwise.</summary>
internal static class ReadApiData
{
    public static Category Category(
        string slug,
        string? name = null,
        Category? parent = null,
        bool isActive = true,
        int sortOrder = 0) =>
        new()
        {
            Name = name ?? slug,
            Slug = slug,
            ParentId = parent?.Id,
            IsActive = isActive,
            SortOrder = sortOrder,
        };

    public static Product Product(
        Category category,
        string slug,
        string? name = null,
        ProductStatus status = ProductStatus.Published,
        FulfilmentType fulfilmentType = FulfilmentType.InStock,
        long price = 1000,
        int? stock = 5)
    {
        var product = new Product
        {
            CategoryId = category.Id,
            Name = name ?? slug,
            Slug = slug,
            ShortDescription = $"Short {slug}",
            Description = $"Long {slug}",
            FulfilmentType = fulfilmentType,
            LeadTimeDays = fulfilmentType == FulfilmentType.MadeToOrder ? 7 : null,
            Status = status,
        };
        Variant(product, $"{slug}-sku", price, stock);
        return product;
    }

    public static ProductVariant Variant(
        Product product,
        string sku,
        long price = 1000,
        int? stock = 5,
        bool isActive = true,
        int sortOrder = 0)
    {
        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Sku = sku,
            Name = sku,
            Price = price,
            StockQuantity = stock,
            WeightGrams = 100,
            IsActive = isActive,
            SortOrder = sortOrder,
            Attributes = new Dictionary<string, string> { ["color"] = "blue" },
        };
        product.Variants.Add(variant);
        return variant;
    }

    public static ProductImage Image(
        Product product,
        string key,
        bool isPrimary = false,
        int sortOrder = 0,
        Guid? variantId = null)
    {
        var image = new ProductImage
        {
            ProductId = product.Id,
            VariantId = variantId,
            StorageKey = key,
            AltText = $"alt {key}",
            IsPrimary = isPrimary,
            SortOrder = sortOrder,
        };
        product.Images.Add(image);
        return image;
    }

    public static void AddAll(StorefrontDbContext context, IEnumerable<Category> categories, IEnumerable<Product> products)
    {
        // Parents must be inserted before their children because of the foreign key.
        context.Categories.AddRange(categories.OrderBy(c => c.ParentId is null ? 0 : 1));
        context.Products.AddRange(products);
    }
}
