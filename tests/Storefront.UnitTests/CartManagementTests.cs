using Storefront.Application.Carts;
using Storefront.Application.Exceptions;

namespace Storefront.UnitTests;

public sealed class CartManagementTests
{
    private readonly FakeRepositoryManager _repositories = new();

    private CartService Service() => new(_repositories);

    private Guid OrderableVariant()
    {
        var id = Guid.NewGuid();
        _repositories.OrderableVariants.Add(id);
        return id;
    }

    private async Task<(CartService Service, string Token, Guid VariantId)> CartWithOneLineAsync(int quantity = 2)
    {
        var service = Service();
        var variantId = OrderableVariant();
        var change = await service.AddItemAsync(null, variantId, quantity, default);
        return (service, change.Token!, variantId);
    }

    [Fact]
    public async Task Get_WithoutACookie_IsAnEmptyCart_AndCreatesNothing()
    {
        var cart = await Service().GetAsync(null, default);

        Assert.Empty(cart.Lines);
        Assert.Equal(0, cart.ItemCount);
        Assert.Empty(_repositories.Carts);
        Assert.Equal(0, _repositories.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Get_WithAnUnknownOrBrokenCookie_IsAnEmptyCart_NotAnError(string token)
    {
        var cart = await Service().GetAsync(token, default);

        Assert.Empty(cart.Lines);
        Assert.Empty(_repositories.Carts);
    }

    [Fact]
    public async Task Add_WithoutACart_CreatesOneAndReturnsTheNewToken()
    {
        var variantId = OrderableVariant();

        var change = await Service().AddItemAsync(null, variantId, 3, default);

        Assert.NotNull(change.Token);
        var cart = Assert.Single(_repositories.Carts);
        Assert.True(CartToken.TryHash(change.Token, out var hash));
        Assert.Equal(hash, cart.TokenHash);
        var line = Assert.Single(cart.Items);
        Assert.Equal((variantId, 3), (line.VariantId, line.Quantity));
        Assert.Equal(3, change.Cart.ItemCount);
    }

    [Fact]
    public async Task Add_WithAnUnknownCookie_CreatesANewCart_AndDoesNotReuseTheOldValue()
    {
        var variantId = OrderableVariant();
        var unknown = CartToken.Create();

        var change = await Service().AddItemAsync(unknown, variantId, 1, default);

        Assert.NotEqual(unknown, change.Token);
        Assert.Single(_repositories.Carts);
    }

    [Fact]
    public async Task Add_TheSameVariantAgain_AddsTheQuantitiesInOneLine()
    {
        var (service, token, variantId) = await CartWithOneLineAsync(quantity: 2);

        var change = await service.AddItemAsync(token, variantId, 5, default);

        Assert.Equal(token, change.Token);
        var line = Assert.Single(Assert.Single(_repositories.Carts).Items);
        Assert.Equal(7, line.Quantity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public async Task Add_AcceptsOneToTwentyUnitsOfOneVariant(int quantity)
    {
        var variantId = OrderableVariant();

        await Service().AddItemAsync(null, variantId, quantity, default);

        Assert.Equal(quantity, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Fact]
    public async Task Add_RejectsTwentyOneUnitsOfOneVariant_AndCreatesNoCart()
    {
        var variantId = OrderableVariant();

        var error = await Assert.ThrowsAsync<RuleViolationException>(
            () => Service().AddItemAsync(null, variantId, 21, default));

        Assert.Equal("cart.quantity_too_high", error.Code);
        Assert.Empty(_repositories.Carts);
    }

    [Fact]
    public async Task Add_RejectsASum_ThatPassesTwentyAcrossTwoAdds_AndKeepsTheLine()
    {
        var (service, token, variantId) = await CartWithOneLineAsync(quantity: 15);

        var error = await Assert.ThrowsAsync<RuleViolationException>(
            () => service.AddItemAsync(token, variantId, 6, default));

        Assert.Equal("cart.quantity_too_high", error.Code);
        Assert.Equal(15, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Add_RejectsAQuantityBelowOne_WithAFieldError(int quantity)
    {
        var variantId = OrderableVariant();

        var error = await Assert.ThrowsAsync<ValidationFailedException>(
            () => Service().AddItemAsync(null, variantId, quantity, default));

        Assert.Contains("quantity", error.Errors.Keys);
        Assert.Empty(_repositories.Carts);
    }

    [Fact]
    public async Task Add_AVariantThatCannotBeOrdered_IsNotFound_AndCreatesNoCart()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => Service().AddItemAsync(null, Guid.NewGuid(), 1, default));

        Assert.Equal("variant.not_found", error.Code);
        Assert.Empty(_repositories.Carts);
        Assert.Equal(0, _repositories.SaveCount);
    }

    private Guid StockedVariant(int stock)
    {
        var id = OrderableVariant();
        _repositories.StockLimits[id] = stock;
        return id;
    }

    [Fact]
    public async Task Add_AcceptsAsManyUnitsAsAreInStock()
    {
        var variantId = StockedVariant(stock: 3);

        await Service().AddItemAsync(null, variantId, 3, default);

        Assert.Equal(3, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Fact]
    public async Task Add_MoreThanIsInStock_IsRefused_AndCreatesNoCart()
    {
        var variantId = StockedVariant(stock: 3);

        var error = await Assert.ThrowsAsync<RuleViolationException>(
            () => Service().AddItemAsync(null, variantId, 4, default));

        Assert.Equal("cart.quantity_exceeds_stock", error.Code);
        Assert.Empty(_repositories.Carts);
    }

    [Fact]
    public async Task Add_AVariantWithNoStock_IsRefused()
    {
        var variantId = StockedVariant(stock: 0);

        var error = await Assert.ThrowsAsync<RuleViolationException>(
            () => Service().AddItemAsync(null, variantId, 1, default));

        Assert.Equal("cart.quantity_exceeds_stock", error.Code);
        Assert.Empty(_repositories.Carts);
    }

    [Fact]
    public async Task Add_TheSameVariantAgain_ChecksTheSumAgainstTheStock_AndKeepsTheLine()
    {
        var variantId = StockedVariant(stock: 5);
        var service = Service();
        var token = (await service.AddItemAsync(null, variantId, 3, default)).Token;

        var error = await Assert.ThrowsAsync<RuleViolationException>(
            () => service.AddItemAsync(token, variantId, 3, default));

        Assert.Equal("cart.quantity_exceeds_stock", error.Code);
        Assert.Equal(3, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Fact]
    public async Task Add_AMadeToOrderVariant_HasNoStockLimit_OnlyTheTwentyUnitLimit()
    {
        var variantId = OrderableVariant();

        await Service().AddItemAsync(null, variantId, 20, default);

        Assert.Equal(20, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Fact]
    public async Task SetQuantity_AboveTheStock_IsRefused_AndKeepsTheQuantity()
    {
        var variantId = StockedVariant(stock: 4);
        var service = Service();
        var token = (await service.AddItemAsync(null, variantId, 2, default)).Token;

        var error = await Assert.ThrowsAsync<RuleViolationException>(
            () => service.SetQuantityAsync(token, variantId, 5, default));

        Assert.Equal("cart.quantity_exceeds_stock", error.Code);
        Assert.Equal(2, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Fact]
    public async Task SetQuantity_ToTheStock_IsAccepted()
    {
        var variantId = StockedVariant(stock: 4);
        var service = Service();
        var token = (await service.AddItemAsync(null, variantId, 2, default)).Token;

        await service.SetQuantityAsync(token, variantId, 4, default);

        Assert.Equal(4, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Fact]
    public async Task SetQuantity_Down_IsAllowed_EvenWhenTheStockFellBelowTheLine()
    {
        var variantId = StockedVariant(stock: 6);
        var service = Service();
        var token = (await service.AddItemAsync(null, variantId, 6, default)).Token;
        _repositories.StockLimits[variantId] = 2;

        await service.SetQuantityAsync(token, variantId, 4, default);

        Assert.Equal(4, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    private async Task<(CartService Service, string? Token)> CartWithLinesAsync(int lines)
    {
        var service = Service();
        string? token = null;
        for (var i = 0; i < lines; i++)
        {
            token = (await service.AddItemAsync(token, OrderableVariant(), 1, default)).Token;
        }

        return (service, token);
    }

    [Fact]
    public async Task Add_AcceptsTheFiftiethDifferentVariant()
    {
        var (service, token) = await CartWithLinesAsync(49);

        await service.AddItemAsync(token, OrderableVariant(), 1, default);

        Assert.Equal(50, Assert.Single(_repositories.Carts).Items.Count);
    }

    [Fact]
    public async Task Add_RejectsTheFiftyFirstDifferentVariant_AndKeepsFiftyLines()
    {
        var (service, token) = await CartWithLinesAsync(50);

        var error = await Assert.ThrowsAsync<RuleViolationException>(
            () => service.AddItemAsync(token, OrderableVariant(), 1, default));

        Assert.Equal("cart.too_many_items", error.Code);
        Assert.Equal(50, Assert.Single(_repositories.Carts).Items.Count);
    }

    [Fact]
    public async Task Add_ToAFullCart_StillAllowsMoreUnitsOfAVariantAlreadyThere()
    {
        var (service, token, variantId) = await CartWithOneLineAsync(quantity: 1);
        for (var i = 0; i < 49; i++)
        {
            await service.AddItemAsync(token, OrderableVariant(), 1, default);
        }

        await service.AddItemAsync(token, variantId, 1, default);

        Assert.Equal(2, _repositories.Carts.Single().Items.Single(l => l.VariantId == variantId).Quantity);
    }

    [Fact]
    public async Task EveryChangeToAnExistingCart_RefreshesItsUpdatedAt_ButTheFirstAddDoesNot()
    {
        var (service, token, variantId) = await CartWithOneLineAsync();
        Assert.Empty(_repositories.TouchedCarts);

        await service.AddItemAsync(token, OrderableVariant(), 1, default);
        await service.SetQuantityAsync(token, variantId, 4, default);
        await service.RemoveItemAsync(token, variantId, default);
        await service.ClearAsync(token, default);

        Assert.Equal(4, _repositories.TouchedCarts.Count);
    }

    [Fact]
    public async Task SetQuantity_ReplacesTheQuantity()
    {
        var (service, token, variantId) = await CartWithOneLineAsync(quantity: 2);

        var change = await service.SetQuantityAsync(token, variantId, 9, default);

        Assert.Equal(token, change.Token);
        Assert.Equal(9, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Theory]
    [InlineData(0, typeof(ValidationFailedException))]
    [InlineData(21, typeof(RuleViolationException))]
    public async Task SetQuantity_RejectsOutOfRangeValues_AndKeepsTheLine(int quantity, Type expected)
    {
        var (service, token, variantId) = await CartWithOneLineAsync(quantity: 2);

        var error = await Record.ExceptionAsync(() => service.SetQuantityAsync(token, variantId, quantity, default));

        Assert.IsType(expected, error);
        Assert.Equal(2, Assert.Single(Assert.Single(_repositories.Carts).Items).Quantity);
    }

    [Fact]
    public async Task SetQuantityAndRemove_OfALineThatIsNotThere_AreNotFound()
    {
        var (service, token, _) = await CartWithOneLineAsync();

        var set = await Assert.ThrowsAsync<NotFoundException>(
            () => service.SetQuantityAsync(token, Guid.NewGuid(), 1, default));
        var remove = await Assert.ThrowsAsync<NotFoundException>(
            () => service.RemoveItemAsync(token, Guid.NewGuid(), default));
        var noCart = await Assert.ThrowsAsync<NotFoundException>(
            () => service.RemoveItemAsync(null, Guid.NewGuid(), default));

        Assert.All([set.Code, remove.Code, noCart.Code], code => Assert.Equal("cart.item_not_found", code));
    }

    [Fact]
    public async Task Remove_DeletesOnlyThatLine()
    {
        var (service, token, variantId) = await CartWithOneLineAsync();
        var other = OrderableVariant();
        await service.AddItemAsync(token, other, 1, default);

        var change = await service.RemoveItemAsync(token, variantId, default);

        Assert.Equal(other, Assert.Single(Assert.Single(_repositories.Carts).Items).VariantId);
        Assert.Equal(token, change.Token);
    }

    [Fact]
    public async Task Clear_RemovesEveryLine_AndKeepsTheCart()
    {
        var (service, token, _) = await CartWithOneLineAsync();
        await service.AddItemAsync(token, OrderableVariant(), 1, default);

        var change = await service.ClearAsync(token, default);

        Assert.Empty(change.Cart.Lines);
        Assert.Empty(Assert.Single(_repositories.Carts).Items);
        Assert.Equal(token, change.Token);
    }

    [Fact]
    public async Task Clear_WithoutACart_IsAnEmptyCart_WithNoCookie()
    {
        var change = await Service().ClearAsync(null, default);

        Assert.Empty(change.Cart.Lines);
        Assert.Null(change.Token);
        Assert.Equal(0, _repositories.SaveCount);
    }

    [Fact]
    public async Task ACartIsNotVisibleWithAnotherShoppersToken()
    {
        var (service, _, variantId) = await CartWithOneLineAsync();
        var stranger = CartToken.Create();

        var read = await service.GetAsync(stranger, default);
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => service.RemoveItemAsync(stranger, variantId, default));

        Assert.Empty(read.Lines);
        Assert.Equal("cart.item_not_found", error.Code);
        Assert.Single(Assert.Single(_repositories.Carts).Items);
    }
}
