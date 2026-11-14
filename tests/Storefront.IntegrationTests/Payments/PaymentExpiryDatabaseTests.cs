using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Orders;
using Storefront.Application.Payments;
using Storefront.Domain.Orders;
using Storefront.Domain.Payments;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Payments;

/// <summary>The cancellation of unpaid orders (task 26d) against a real PostgreSQL, with a clock the tests move.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PaymentExpiryDatabaseTests(PostgresFixture postgres)
{
    private Task<Setup> CreateAsync(
        int firstStock = 5,
        IReadOnlyDictionary<string, string>? settings = null) =>
        PaymentWorld.CreateAsync(postgres, firstStock, settings: settings);

    private static async Task<PaymentExpiryReport> ExpireAsync(Setup setup)
    {
        await using var scope = setup.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPaymentExpiryService>().CancelExpiredAsync(default);
    }

    // The deadline is 60 minutes and the pause 5, so an order is cancelled once the clock is past 65 minutes.
    private static void PassTheDeadlineAndThePause(Setup setup) =>
        setup.Time.Advance(TimeSpan.FromMinutes(65) + TimeSpan.FromSeconds(1));

    [Fact]
    public async Task AnUnpaidOrderPastItsDeadlineAndThePause_IsCancelled_AndItsStockGoesBack()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));
        PassTheDeadlineAndThePause(setup);

        var report = await ExpireAsync(setup);

        Assert.Equal(new PaymentExpiryReport(1, 0), report);
        var cancelled = await OrderAsync(setup, order.Number);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.StockReleasedAt);
        Assert.Equal(5, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task AnOrderPastItsDeadlineButInsideThePause_IsNotCancelled()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        setup.Time.Advance(TimeSpan.FromMinutes(64));

        var report = await ExpireAsync(setup);

        Assert.Equal(new PaymentExpiryReport(0, 0), report);
        Assert.Equal(OrderStatus.New, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task TheMomentWhenTheDeadlineAndThePauseEnd_IsStillNotCancelled_AndASecondLaterIs()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        setup.Time.Advance(TimeSpan.FromMinutes(65));

        var atTheEdge = await ExpireAsync(setup);
        setup.Time.Advance(TimeSpan.FromSeconds(1));
        var later = await ExpireAsync(setup);

        Assert.Equal(0, atTheEdge.Cancelled);
        Assert.Equal(1, later.Cancelled);
        Assert.Equal(OrderStatus.Cancelled, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task APaidOrder_IsNotTouched_WhateverTheClockSays()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));
        PassTheDeadlineAndThePause(setup);

        var report = await ExpireAsync(setup);

        Assert.Equal(new PaymentExpiryReport(0, 0), report);
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task AnOrderWithoutADeadline_IsNeverCancelled()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        await using (var context = setup.Database.CreateContext())
        {
            await context.Orders.Where(o => o.Number == order.Number)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentDueAt, (DateTimeOffset?)null));
        }

        setup.Time.Advance(TimeSpan.FromDays(30));
        var report = await ExpireAsync(setup);

        Assert.Equal(new PaymentExpiryReport(0, 0), report);
        Assert.Equal(OrderStatus.New, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task APaymentTurnedOff_LeavesNoDeadlineOnTheOrder()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, paymentEnabled: false);

        var order = await PlaceAsync(setup);

        Assert.Null(order.PaymentDueAt);
        Assert.Null((await OrderAsync(setup, order.Number)).PaymentDueAt);
    }

    [Fact]
    public async Task ASecondRun_DoesNothingMore_AndTheStockIsGivenBackOnce()
    {
        await using var setup = await CreateAsync();
        await PlaceAsync(setup);
        PassTheDeadlineAndThePause(setup);

        var first = await ExpireAsync(setup);
        var second = await ExpireAsync(setup);

        Assert.Equal(1, first.Cancelled);
        Assert.Equal(new PaymentExpiryReport(0, 0), second);
        Assert.Equal(5, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task ManyExpiredOrders_AreAllCancelled_AcrossSeveralBatches()
    {
        await using var setup = await CreateAsync(
            firstStock: 100,
            settings: new Dictionary<string, string> { ["Payment:ExpiryBatchSize"] = "2" });
        for (var i = 0; i < 5; i++)
        {
            await PlaceAsync(setup);
        }

        PassTheDeadlineAndThePause(setup);
        var report = await ExpireAsync(setup);

        Assert.Equal(new PaymentExpiryReport(5, 0), report);
        Assert.Equal(100, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task AFailureOfOneOrder_RollsBackThatOrderOnly_AndTheNextRunTriesItAgain()
    {
        await using var setup = await CreateAsync(firstStock: 10);
        var bad = await PlaceAsync(setup);
        var good = await PlaceAsync(setup);
        PassTheDeadlineAndThePause(setup);
        await using (var context = setup.Database.CreateContext())
        {
            // The database refuses to change the first order, as a disk or a lock failure would.
            await context.Database.ExecuteSqlRawAsync(
                "CREATE FUNCTION refuse_order() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RAISE EXCEPTION ''refused''; END;'");
#pragma warning disable EF1002 // The number is an int made by the test; DDL cannot take a parameter.
            await context.Database.ExecuteSqlRawAsync(
                $"CREATE TRIGGER refuse BEFORE UPDATE ON orders FOR EACH ROW WHEN (OLD.number = {bad.Number}) EXECUTE FUNCTION refuse_order()");
#pragma warning restore EF1002
        }

        var report = await ExpireAsync(setup);

        Assert.Equal(new PaymentExpiryReport(1, 1), report);
        Assert.Equal(OrderStatus.Cancelled, (await OrderAsync(setup, good.Number)).Status);
        var kept = await OrderAsync(setup, bad.Number);
        Assert.Equal(OrderStatus.New, kept.Status);
        Assert.Null(kept.StockReleasedAt);
        Assert.Equal(8, await StockOfAsync(setup, FirstVariantId));

        await using (var context = setup.Database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER refuse ON orders");
        }

        var again = await ExpireAsync(setup);

        Assert.Equal(new PaymentExpiryReport(1, 0), again);
        Assert.Equal(10, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task ThePaymentAndTheExpiryAtTheSameMoment_LeaveEveryOrderPaid_AndTheStockExact()
    {
        const int orders = 8;
        await using var setup = await CreateAsync(
            firstStock: 100,
            settings: new Dictionary<string, string> { ["Payment:GraceMinutes"] = "0" });
        var placed = new List<OrderDto>();
        for (var i = 0; i < orders; i++)
        {
            placed.Add(await PlaceAsync(setup));
        }

        // Signed a second before the deadline, when the provider still takes the payment; handled just after it.
        setup.Time.Advance(TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59));
        var messages = new List<PaymentMessage>();
        foreach (var order in placed)
        {
            var (_, reference) = await OpenAsync(setup, order);
            messages.Add(await MessageAsync(setup, reference, PaymentOutcome.Paid));
        }

        setup.Time.Advance(TimeSpan.FromSeconds(2));
        var start = new TaskCompletionSource();
        var work = messages.Select(message => Task.Run(async () =>
        {
            await start.Task;
            return await SendAsync(setup, message);
        })).ToList();
        var expiry = Task.Run(async () =>
        {
            await start.Task;
            return await ExpireAsync(setup);
        });
        start.SetResult();
        await Task.WhenAll(work);
        await expiry;

        // Whoever won, a paid message either paid the order first or brought it back after the expiry cancelled it.
        foreach (var order in placed)
        {
            var current = await OrderAsync(setup, order.Number);
            Assert.Equal(OrderStatus.Paid, current.Status);
            Assert.Null(current.StockReleasedAt);
        }

        Assert.All(await PaymentsAsync(setup), payment => Assert.Equal(PaymentStatus.Succeeded, payment.Status));
        Assert.Equal(100 - (orders * 2), await StockOfAsync(setup, FirstVariantId));
    }
}
