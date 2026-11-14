using Microsoft.EntityFrameworkCore;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Application.Payments;
using Storefront.Domain.Orders;
using Storefront.IntegrationTests.Payments;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>The administrator's change of status and refusal of an order (task 28b) against a real PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderAdminActionsDatabaseTests(PostgresFixture postgres)
{
    private const int Stock = 5;

    private Task<Setup> WorldAsync() => PaymentWorld.CreateAsync(postgres, firstStock: Stock, secondStock: Stock);

    private static Task<OrderAdminDto> ChangeAsync(Setup setup, int number, OrderStatus to) =>
        InScopeAsync(setup, s => s.OrderAdmin.ChangeStatusAsync(number, to, default));

    private static Task<OrderCancelledDto> CancelAsync(Setup setup, int number) =>
        InScopeAsync(setup, s => s.OrderAdmin.CancelAsync(number, default));

    private static async Task<OrderDto> PaidOrderAsync(Setup setup)
    {
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));
        return order;
    }

    private static async Task SetStatusAsync(Setup setup, int number, OrderStatus status)
    {
        await using var context = setup.Database.CreateContext();
        await context.Orders.Where(o => o.Number == number).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status));
    }

    [Fact]
    public async Task APaidOrder_WalksThroughProductionToCompleted_OneStepAtATime()
    {
        await using var setup = await WorldAsync();
        var order = await PaidOrderAsync(setup);

        foreach (var status in new[] { OrderStatus.InProduction, OrderStatus.ReadyToShip, OrderStatus.Shipped, OrderStatus.Completed })
        {
            var result = await ChangeAsync(setup, order.Number, status);
            Assert.Equal(status.ToString(), result.Order.Status);
        }

        Assert.Equal(OrderStatus.Completed, (await OrderAsync(setup, order.Number)).Status);
    }

    [Theory]
    [InlineData(OrderStatus.New, OrderStatus.Paid)]
    [InlineData(OrderStatus.New, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public async Task AMoveThatIsNotAProductionStep_IsRefused_AndTheStatusStays(OrderStatus from, OrderStatus to)
    {
        await using var setup = await WorldAsync();
        var order = await PlaceAsync(setup);
        await SetStatusAsync(setup, order.Number, from);

        var error = await Assert.ThrowsAsync<ConflictException>(() => ChangeAsync(setup, order.Number, to));

        Assert.Equal("order.status_transition_invalid", error.Code);
        Assert.Equal(from, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task AnUnknownOrder_IsNotFound_ForTheChangeAndForTheRefusal()
    {
        await using var setup = await WorldAsync();

        var change = await Assert.ThrowsAsync<NotFoundException>(() => ChangeAsync(setup, 12345678, OrderStatus.InProduction));
        var cancel = await Assert.ThrowsAsync<NotFoundException>(() => CancelAsync(setup, 12345678));

        Assert.Equal("order.not_found", change.Code);
        Assert.Equal("order.not_found", cancel.Code);
    }

    [Fact]
    public async Task RefusingANewOrder_GivesTheStockBack_AndNeedsNoRefund()
    {
        await using var setup = await WorldAsync();
        var order = await PlaceAsync(setup);
        Assert.Equal(Stock - 2, await StockOfAsync(setup, FirstVariantId));

        var result = await CancelAsync(setup, order.Number);

        Assert.Equal("Cancelled", result.Order.Order.Status);
        Assert.False(result.RefundByHand);
        Assert.Equal(Stock, await StockOfAsync(setup, FirstVariantId));
        Assert.NotNull((await OrderAsync(setup, order.Number)).StockReleasedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.InProduction)]
    [InlineData(OrderStatus.ReadyToShip)]
    public async Task RefusingAPaidOrder_GivesTheStockBack_AndSaysTheMoneyGoesBackByHand(OrderStatus status)
    {
        await using var setup = await WorldAsync();
        var order = await PaidOrderAsync(setup);
        await SetStatusAsync(setup, order.Number, status);

        var result = await CancelAsync(setup, order.Number);

        Assert.Equal("Cancelled", result.Order.Order.Status);
        Assert.True(result.RefundByHand);
        Assert.Equal(Stock, await StockOfAsync(setup, FirstVariantId));
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task AnOrderThatLeftOrIsAlreadyCancelled_CannotBeRefused_AndKeepsItsStock(OrderStatus status)
    {
        await using var setup = await WorldAsync();
        var order = await PlaceAsync(setup);
        await SetStatusAsync(setup, order.Number, status);

        var error = await Assert.ThrowsAsync<ConflictException>(() => CancelAsync(setup, order.Number));

        Assert.Equal("order.status_transition_invalid", error.Code);
        Assert.Equal(status, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(Stock - 2, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task TwoRefusalsAtTheSameMoment_HaveOneWinner_AndTheStockGoesBackOnce()
    {
        await using var setup = await WorldAsync();
        var order = await PlaceAsync(setup);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await CancelAsync(setup, order.Number);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }));

        Assert.Single(results, won => won);
        Assert.Equal(Stock, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task TheStatusService_RoutesAMoveToCancelled_ThroughTheRefusal_SoTheStockIsNeverLeftTaken()
    {
        await using var setup = await WorldAsync();
        var order = await PlaceAsync(setup);

        await InScopeAsync(setup, s => s.OrderStatuses.ChangeAsync(order.Number, OrderStatus.Cancelled, default));

        Assert.Equal(OrderStatus.Cancelled, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(Stock, await StockOfAsync(setup, FirstVariantId));
    }
}
