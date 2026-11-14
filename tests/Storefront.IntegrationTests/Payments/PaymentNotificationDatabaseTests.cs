using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Catalog;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Application.Payments;
using Storefront.Domain.Catalog;
using Storefront.Domain.Orders;
using Storefront.Domain.Payments;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Payments;

/// <summary>
/// Paying an order (task 26c) against a real PostgreSQL. Every call gets its own scope, like a separate request, and the
/// clock is a fake one that starts at the real time, so the deadline can be moved without waiting.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PaymentNotificationDatabaseTests(PostgresFixture postgres)
{
    private Task<Setup> CreateAsync(int firstStock = 5, int secondStock = 5, bool paymentEnabled = true) =>
        PaymentWorld.CreateAsync(postgres, firstStock, secondStock, paymentEnabled);

    [Fact]
    public async Task Notification_Paid_MakesTheOrderPaid_AndThePaymentSucceeded()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));

        Assert.Equal(PaymentNotificationResult.Applied, result);
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(setup, order.Number)).Status);
        var payment = Assert.Single(await PaymentsAsync(setup));
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.NotNull(payment.CompletedAt);
        Assert.Equal(1, await EventCountAsync(setup));
        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task Notification_Repeated_ChangesNothing_AndDoesNotPayASecondTime()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        var message = await MessageAsync(setup, reference, PaymentOutcome.Paid, eventId: "evt-1");

        var first = await SendAsync(setup, message);
        var second = await SendAsync(setup, message);

        Assert.Equal(PaymentNotificationResult.Applied, first);
        Assert.Equal(PaymentNotificationResult.Repeated, second);
        Assert.Equal(1, await EventCountAsync(setup));
        Assert.Single(await PaymentsAsync(setup));
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task Notification_RepeatedManyTimesAtTheSameMoment_IsHandledOnce()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        var message = await MessageAsync(setup, reference, PaymentOutcome.Paid, eventId: "evt-1");

        var start = new TaskCompletionSource();
        var calls = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await SendAsync(setup, message);
        })).ToList();
        start.SetResult();
        var results = await Task.WhenAll(calls);

        Assert.Single(results, result => result == PaymentNotificationResult.Applied);
        Assert.Equal(7, results.Count(result => result == PaymentNotificationResult.Repeated));
        Assert.Equal(1, await EventCountAsync(setup));
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task Notification_Failed_KeepsTheOrderNew_AndLetsTheBuyerTryAgain()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (first, reference) = await OpenAsync(setup, order);

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Failed));
        var second = await StartAsync(setup, order.Number);

        Assert.Equal(PaymentNotificationResult.Applied, result);
        Assert.NotEqual(first.PaymentId, second.PaymentId);
        Assert.Equal(OrderStatus.New, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(
            [PaymentStatus.Failed, PaymentStatus.Pending],
            (await PaymentsAsync(setup)).Select(p => p.Status));
    }

    [Fact]
    public async Task Notification_WithADifferentSum_DoesNotPayTheOrder()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid, amount: 1));

        Assert.Equal(PaymentNotificationResult.AmountMismatch, result);
        Assert.Equal(OrderStatus.New, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(PaymentStatus.Pending, Assert.Single(await PaymentsAsync(setup)).Status);
    }

    [Fact]
    public async Task Notification_ForASessionWeDoNotKnow_ChangesNothing()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        await OpenAsync(setup, order);

        var result = await SendAsync(setup, await MessageAsync(setup, "test_0_unknown", PaymentOutcome.Failed));

        Assert.Equal(PaymentNotificationResult.UnknownPayment, result);
        Assert.Equal(0, await EventCountAsync(setup));
        Assert.Equal(PaymentStatus.Pending, Assert.Single(await PaymentsAsync(setup)).Status);
    }

    [Fact]
    public async Task Notification_WithAWrongSignature_IsRefused_AndChangesNothing()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        var message = await MessageAsync(setup, reference, PaymentOutcome.Paid);

        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() =>
            SendAsync(setup, message with { Signature = "t=1,v1=00" }));
        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() =>
            SendAsync(setup, message with { Signature = null }));

        Assert.Equal(OrderStatus.New, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(0, await EventCountAsync(setup));
    }

    [Fact]
    public async Task Notification_AfterThePaymentWasAnswered_ChangesNothingMore()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Failed));

        Assert.Equal(PaymentNotificationResult.Ignored, result);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(await PaymentsAsync(setup)).Status);
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(setup, order.Number)).Status);
    }

    [Fact]
    public async Task Notification_PaidAfterTheOrderWasCancelled_TakesTheStockAgainAndPaysTheOrder()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup, withSecond: true);
        var (_, reference) = await OpenAsync(setup, order);
        await CancelAsync(setup, order);
        Assert.Equal(5, await StockOfAsync(setup, FirstVariantId));

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid, amount: 4500));

        Assert.Equal(PaymentNotificationResult.Revived, result);
        var revived = await OrderAsync(setup, order.Number);
        Assert.Equal(OrderStatus.Paid, revived.Status);
        Assert.Null(revived.StockReleasedAt);
        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));
        Assert.Equal(4, await StockOfAsync(setup, SecondVariantId));
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(await PaymentsAsync(setup)).Status);
    }

    [Fact]
    public async Task Notification_PaidAfterTheOrderWasCancelled_WhenTheStockIsGone_KeepsItCancelledAndGivesEverythingBack()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup, withSecond: true);
        var (_, reference) = await OpenAsync(setup, order);
        await CancelAsync(setup, order);
        await using (var context = setup.Database.CreateContext())
        {
            // The first line could be taken again, the second cannot: nothing may stay taken.
            await context.ProductVariants.Where(v => v.Id == SecondVariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, 0));
        }

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid, amount: 4500));

        Assert.Equal(PaymentNotificationResult.PaidWithoutOrder, result);
        var kept = await OrderAsync(setup, order.Number);
        Assert.Equal(OrderStatus.Cancelled, kept.Status);
        Assert.NotNull(kept.StockReleasedAt);
        Assert.Equal(5, await StockOfAsync(setup, FirstVariantId));
        Assert.Equal(0, await StockOfAsync(setup, SecondVariantId));
        Assert.Equal(PaymentStatus.PaidWithoutOrder, Assert.Single(await PaymentsAsync(setup)).Status);
        Assert.Equal(1, await EventCountAsync(setup));
    }

    [Fact]
    public async Task Notification_PaidAfterTheOrderWasCancelled_ManyTimesAtTheSameMoment_TakesTheStockOnce()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        await CancelAsync(setup, order);
        var message = await MessageAsync(setup, reference, PaymentOutcome.Paid, eventId: "evt-late");

        var start = new TaskCompletionSource();
        var calls = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await SendAsync(setup, message);
        })).ToList();
        start.SetResult();
        var results = await Task.WhenAll(calls);

        Assert.Single(results, result => result == PaymentNotificationResult.Revived);
        Assert.Equal(3, await StockOfAsync(setup, FirstVariantId));
    }

    [Fact]
    public async Task CompleteTest_PaysWithTheSumOfThePayment_ThroughTheSameRoadAsAMessage()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);

        var result = await InScopeAsync(setup, s => s.Payments.CompleteTestPaymentAsync(reference, PaymentOutcome.Paid, default));

        Assert.Equal(PaymentNotificationResult.Applied, result);
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(setup, order.Number)).Status);
        Assert.Equal(1, await EventCountAsync(setup));
    }

    [Fact]
    public async Task CompleteTest_OfAnUnknownSession_IsNotFound()
    {
        await using var setup = await CreateAsync();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            InScopeAsync(setup, s => s.Payments.CompleteTestPaymentAsync("test_0_unknown", PaymentOutcome.Paid, default)));

        Assert.Equal("payment.not_found", exception.Code);
    }

    [Fact]
    public async Task CompleteTest_AfterTheSessionEnded_RefusesAPayment_ButAcceptsAFailure()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        setup.Time.Advance(TimeSpan.FromMinutes(61));

        var exception = await Assert.ThrowsAsync<RuleViolationException>(() =>
            InScopeAsync(setup, s => s.Payments.CompleteTestPaymentAsync(reference, PaymentOutcome.Paid, default)));
        var failed = await InScopeAsync(setup, s => s.Payments.CompleteTestPaymentAsync(reference, PaymentOutcome.Failed, default));

        Assert.Equal("payment.session_expired", exception.Code);
        Assert.Equal(PaymentNotificationResult.Applied, failed);
        Assert.Equal(OrderStatus.New, (await OrderAsync(setup, order.Number)).Status);
    }
}
