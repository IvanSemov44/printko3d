using Storefront.Application.Carts.Queries;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class CartLineRulesTests
{
    [Theory]
    [InlineData(ProductStatus.Published, FulfilmentType.InStock, true, 5, 5, CartLineState.Available)]
    [InlineData(ProductStatus.Published, FulfilmentType.InStock, true, 5, 6, CartLineState.InsufficientStock)]
    [InlineData(ProductStatus.Published, FulfilmentType.InStock, true, 0, 1, CartLineState.InsufficientStock)]
    [InlineData(ProductStatus.Published, FulfilmentType.InStock, true, null, 1, CartLineState.InsufficientStock)]
    [InlineData(ProductStatus.Published, FulfilmentType.MadeToOrder, true, null, 3, CartLineState.Available)]
    [InlineData(ProductStatus.Published, FulfilmentType.InStock, false, 5, 1, CartLineState.Inactive)]
    [InlineData(ProductStatus.Draft, FulfilmentType.InStock, true, 5, 1, CartLineState.Inactive)]
    [InlineData(ProductStatus.Archived, FulfilmentType.InStock, true, 5, 1, CartLineState.Archived)]
    [InlineData(ProductStatus.Archived, FulfilmentType.InStock, false, 5, 1, CartLineState.Archived)]
    public void StateOf_FollowsTheRulesOfTheCart(
        ProductStatus status, FulfilmentType fulfilment, bool isActive, int? stock, int quantity, CartLineState expected) =>
        Assert.Equal(expected, CartLineRules.StateOf(status, fulfilment, isActive, stock, quantity));

    [Fact]
    public void Total_AddsOnlyTheAvailableLines_AndTheCountAddsAllUnits()
    {
        CartLineDto Line(int quantity, CartLineState state) =>
            new(Guid.NewGuid(), "P", "p", "V", "S", null, 100, quantity, 100 * quantity, state, FulfilmentType.InStock, null, 10);

        var cart = CartDto.From([
            Line(2, CartLineState.Available),
            Line(3, CartLineState.InsufficientStock),
            Line(1, CartLineState.Archived),
        ]);

        Assert.Equal(200, cart.Total);
        Assert.Equal(6, cart.ItemCount);
    }
}
