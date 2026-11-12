using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Carts;
using Storefront.Application.Carts.Queries;
using Storefront.Application.Catalog;
using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Carts;
using Storefront.Domain.Catalog;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>The cart service against a real PostgreSQL. Every call gets its own scope, like a separate request.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CartServiceDatabaseTests(PostgresFixture postgres)
{
    private sealed record Setup(TestDatabase Database, ApiFactory Factory, Guid ProductId, Guid VariantId)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private async Task<Setup> CreateAsync(
        ProductStatus status = ProductStatus.Published,
        FulfilmentType fulfilment = FulfilmentType.InStock,
        int? stock = 5,
        bool isVariantActive = true,
        bool withImage = true)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(
            category.Id,
            fulfilmentType: fulfilment,
            leadTimeDays: fulfilment == FulfilmentType.MadeToOrder ? 7 : null);
        product.Status = status;
        var variant = CatalogData.Variant(product);
        variant.StockQuantity = stock;
        variant.IsActive = isVariantActive;
        if (withImage)
        {
            CatalogData.Image(product, "images/dragon.jpg", isPrimary: true);
        }

        context.Add(category);
        context.Add(product);
        await context.SaveChangesAsync();
        return new Setup(database, new ApiFactory(database.ConnectionString), product.Id, variant.Id);
    }

    private static async Task<T> InScopeAsync<T>(Setup setup, Func<IServiceManager, Task<T>> action)
    {
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IServiceManager>());
    }

    private static async Task<Guid> AddProductAsync(Setup setup, string slug, string sku)
    {
        await using var context = setup.Database.CreateContext();
        var categoryId = (await context.Categories.AsNoTracking().FirstAsync()).Id;
        var product = CatalogData.Product(categoryId, slug);
        product.Status = ProductStatus.Published;
        var variant = CatalogData.Variant(product, sku);
        context.Add(product);
        await context.SaveChangesAsync();
        return variant.Id;
    }

    [Fact]
    public async Task Add_ThenGet_ShowsTheLineWithCurrentPriceNameAndImage()
    {
        await using var setup = await CreateAsync();

        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 2, default));
        var cart = await InScopeAsync(setup, s => s.Cart.GetAsync(change.Token, default));

        var line = Assert.Single(cart.Lines);
        Assert.Equal(
            (setup.VariantId, "Dragon", "dragon", "Blue", "DRG-001", 1500L, 2, 3000L, CartLineState.Available),
            (line.VariantId, line.ProductName, line.ProductSlug, line.VariantName, line.Sku, line.UnitPrice,
                line.Quantity, line.LinePrice, line.State));
        Assert.Equal("images/dragon.jpg", line.Image?.StorageKey);
        Assert.Equal((2, 3000L), (cart.ItemCount, cart.Total));
    }

    [Fact]
    public async Task APriceChangeOfTheVariant_ShowsInTheNextRead_NothingIsStoredInTheLine()
    {
        await using var setup = await CreateAsync();
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 2, default));

        await using (var context = setup.Database.CreateContext())
        {
            await context.ProductVariants.Where(v => v.Id == setup.VariantId).ExecuteUpdateAsync(
                u => u.SetProperty(v => v.Price, 1999L).SetProperty(v => v.Name, "Red"));
        }

        var line = Assert.Single((await InScopeAsync(setup, s => s.Cart.GetAsync(change.Token, default))).Lines);
        Assert.Equal((1999L, 3998L, "Red"), (line.UnitPrice, line.LinePrice, line.VariantName));
    }

    [Theory]
    [InlineData(ProductStatus.Archived, true, 5, CartLineState.Archived)]
    [InlineData(ProductStatus.Draft, true, 5, CartLineState.Inactive)]
    [InlineData(ProductStatus.Published, false, 5, CartLineState.Inactive)]
    [InlineData(ProductStatus.Published, true, 1, CartLineState.InsufficientStock)]
    [InlineData(ProductStatus.Published, true, 2, CartLineState.Available)]
    public async Task ALineChangesStateWhenTheCatalogChanges_AndOnlyAvailableLinesCountInTheTotal(
        ProductStatus status, bool isActive, int stock, CartLineState expected)
    {
        await using var setup = await CreateAsync();
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 2, default));
        await using (var context = setup.Database.CreateContext())
        {
            await context.Products.Where(p => p.Id == setup.ProductId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, status));
            await context.ProductVariants.Where(v => v.Id == setup.VariantId).ExecuteUpdateAsync(
                u => u.SetProperty(v => v.IsActive, isActive).SetProperty(v => v.StockQuantity, (int?)stock));
        }

        var cart = await InScopeAsync(setup, s => s.Cart.GetAsync(change.Token, default));

        Assert.Equal(expected, Assert.Single(cart.Lines).State);
        Assert.Equal(expected == CartLineState.Available ? 3000L : 0L, cart.Total);
        Assert.Equal(2, cart.ItemCount);
    }

    [Fact]
    public async Task AMadeToOrderLine_IsAvailableWithoutStock()
    {
        await using var setup = await CreateAsync(fulfilment: FulfilmentType.MadeToOrder, stock: null);
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 3, default));

        var cart = await InScopeAsync(setup, s => s.Cart.GetAsync(change.Token, default));

        Assert.Equal(CartLineState.Available, Assert.Single(cart.Lines).State);
    }

    [Fact]
    public async Task ALineWithoutAnImage_HasNoImage()
    {
        await using var setup = await CreateAsync(withImage: false);
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 1, default));

        var cart = await InScopeAsync(setup, s => s.Cart.GetAsync(change.Token, default));

        Assert.Null(Assert.Single(cart.Lines).Image);
    }

    [Theory]
    [InlineData(ProductStatus.Draft, true)]
    [InlineData(ProductStatus.Archived, true)]
    [InlineData(ProductStatus.Published, false)]
    public async Task AVariantThatCannotBeOrdered_IsNotFound_AndNoCartIsStored(ProductStatus status, bool isActive)
    {
        await using var setup = await CreateAsync(status: status, isVariantActive: isActive);

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 1, default)));

        Assert.Equal("variant.not_found", error.Code);
        await using var context = setup.Database.CreateContext();
        Assert.Empty(await context.Carts.ToListAsync());
    }

    [Fact]
    public async Task ChangesPersist_AndRefreshUpdatedAtOfTheCart()
    {
        await using var setup = await CreateAsync();
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 2, default));
        var before = await CartUpdatedAtAsync(setup);

        await Task.Delay(20);
        await InScopeAsync(setup, s => s.Cart.AddItemAsync(change.Token, setup.VariantId, 3, default));
        var afterAdd = await CartUpdatedAtAsync(setup);
        await Task.Delay(20);
        await InScopeAsync(setup, s => s.Cart.SetQuantityAsync(change.Token, setup.VariantId, 7, default));
        var afterSet = await CartUpdatedAtAsync(setup);

        Assert.True(afterAdd > before);
        Assert.True(afterSet > afterAdd);
        await using var context = setup.Database.CreateContext();
        Assert.Equal(7, (await context.CartItems.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task RemoveAndClear_DeleteLinesInTheDatabase_AndKeepTheCart()
    {
        await using var setup = await CreateAsync();
        var otherVariant = await AddProductAsync(setup, "phoenix", "PHX-001");
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 1, default));
        await InScopeAsync(setup, s => s.Cart.AddItemAsync(change.Token, otherVariant, 1, default));

        await InScopeAsync(setup, s => s.Cart.RemoveItemAsync(change.Token, setup.VariantId, default));
        await using (var context = setup.Database.CreateContext())
        {
            Assert.Equal(otherVariant, (await context.CartItems.SingleAsync()).VariantId);
        }

        var cleared = await InScopeAsync(setup, s => s.Cart.ClearAsync(change.Token, default));

        Assert.Empty(cleared.Cart.Lines);
        await using var after = setup.Database.CreateContext();
        Assert.Empty(await after.CartItems.ToListAsync());
        Assert.Single(await after.Carts.ToListAsync());
    }

    [Fact]
    public async Task OnlyTheHashOfTheTokenIsStored()
    {
        await using var setup = await CreateAsync();
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 1, default));

        await using var context = setup.Database.CreateContext();
        var stored = await context.Carts.AsNoTracking().SingleAsync();

        Assert.True(CartToken.TryHash(change.Token, out var hash));
        Assert.Equal(hash, stored.TokenHash);
        Assert.NotEqual(change.Token, Convert.ToBase64String(stored.TokenHash));
    }

    [Fact]
    public async Task TwoShoppersWhoAddTheSameNewVariantAtOnce_GetAConflictOnTheSecondSave_NotAServerError()
    {
        await using var setup = await CreateAsync();
        var otherVariant = await AddProductAsync(setup, "phoenix", "PHX-001");
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 1, default));
        CartToken.TryHash(change.Token, out var hash);

        // Both "tabs" read the cart before either saves, then both add the same new variant.
        await using var first = setup.Factory.Services.CreateAsyncScope();
        await using var second = setup.Factory.Services.CreateAsyncScope();
        var firstRepositories = first.ServiceProvider.GetRequiredService<IRepositoryManager>();
        var secondRepositories = second.ServiceProvider.GetRequiredService<IRepositoryManager>();
        var firstCart = (await firstRepositories.Cart.GetByTokenHashAsync(hash, true, default))!;
        var secondCart = (await secondRepositories.Cart.GetByTokenHashAsync(hash, true, default))!;
        firstRepositories.Cart.AddItem(new CartItem { CartId = firstCart.Id, VariantId = otherVariant, Quantity = 1 });
        secondRepositories.Cart.AddItem(new CartItem { CartId = secondCart.Id, VariantId = otherVariant, Quantity = 2 });

        await firstRepositories.SaveAsync(default);
        var error = await Assert.ThrowsAsync<ConflictException>(() => secondRepositories.SaveAsync(default));

        Assert.Equal("cart.item_conflict", error.Code);
        await using var context = setup.Database.CreateContext();
        Assert.Equal(1, await context.CartItems.CountAsync(i => i.VariantId == otherVariant));
    }

    [Fact]
    public async Task AnotherShoppersToken_SeesAnEmptyCart_AndCannotChangeTheCart()
    {
        await using var setup = await CreateAsync();
        await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, setup.VariantId, 1, default));
        var stranger = CartToken.Create();

        var cart = await InScopeAsync(setup, s => s.Cart.GetAsync(stranger, default));
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => InScopeAsync(setup, s => s.Cart.RemoveItemAsync(stranger, setup.VariantId, default)));

        Assert.Empty(cart.Lines);
        Assert.Equal("cart.item_not_found", error.Code);
        await using var context = setup.Database.CreateContext();
        Assert.Single(await context.CartItems.ToListAsync());
    }

    private static async Task<DateTimeOffset> CartUpdatedAtAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return (await context.Carts.AsNoTracking().SingleAsync()).UpdatedAt;
    }
}
