using Microsoft.EntityFrameworkCore;
using Storefront.Application.Exceptions;
using Storefront.Domain.Orders;
using Storefront.IntegrationTests.Payments;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>Moving an order between statuses (task 27a) against a real PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderStatusDatabaseTests(PostgresFixture postgres)
{
    private static Task<Storefront.Application.Orders.OrderDto> ChangeAsync(Setup setup, int number, OrderStatus to) =>
        InScopeAsync(setup, s => s.OrderStatuses.ChangeAsync(number, to, default));

    private static async Task<Storefront.Application.Orders.OrderDto> PaidOrderAsync(Setup setup)
    {
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        await SendAsync(setup, await MessageAsync(setup, reference, Storefront.Application.Payments.PaymentOutcome.Paid));
        return order;
    }

    [Fact]
    public async Task APaidOrder_WalksThroughProductionToCompleted()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PaidOrderAsync(setup);

        foreach (var status in new[] { OrderStatus.InProduction, OrderStatus.ReadyToShip, OrderStatus.Shipped, OrderStatus.Completed })
        {
            var result = await ChangeAsync(setup, order.Number, status);
            Assert.Equal(status.ToString(), result.Status);
            Assert.Equal(status, (await OrderAsync(setup, order.Number)).Status);
        }
    }

    [Fact]
    public async Task ANewOrder_CannotBeShipped_AndStaysNew()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);

        var error = await Assert.ThrowsAsync<ConflictException>(() => ChangeAsync(setup, order.Number, OrderStatus.Shipped));

        Assert.Equal("order.status_transition_invalid", error.Code);
        Assert.Equal(OrderStatus.New, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task AnUnknownOrder_IsNotFound()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => ChangeAsync(setup, 12345678, OrderStatus.Paid));

        Assert.Equal("order.not_found", error.Code);
    }

    [Fact]
    public async Task TwoMovesAtTheSameMoment_HaveOneWinner()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PaidOrderAsync(setup);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await ChangeAsync(setup, order.Number, OrderStatus.InProduction);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }));

        Assert.Single(results, won => won);
        Assert.Equal(OrderStatus.InProduction, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task TheDatabase_RefusesAStatusThatDoesNotExist()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);

        await using var context = setup.Database.CreateContext();
        var error = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE orders SET status = 'Teleported' WHERE number = {0}", order.Number));

        Assert.Contains("ck_orders_status", error.ToString());
    }
}
