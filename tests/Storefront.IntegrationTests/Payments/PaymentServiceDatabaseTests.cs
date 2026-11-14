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
public sealed class PaymentServiceDatabaseTests(PostgresFixture postgres)
{
    private Task<Setup> CreateAsync(int firstStock = 5, int secondStock = 5, bool paymentEnabled = true) =>
        PaymentWorld.CreateAsync(postgres, firstStock, secondStock, paymentEnabled);


    [Fact]
    public async Task Place_SetsTheDeadlineOfThePayment_FromTheWindow()
    {
        await using var setup = await CreateAsync();

        var order = await PlaceAsync(setup);

        Assert.Equal(setup.Time.GetUtcNow().AddMinutes(60), order.PaymentDueAt!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Equal(order.PaymentDueAt.Value, (await OrderAsync(setup, order.Number)).PaymentDueAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Start_OpensAPaymentWithTheSumOfTheOrder_AndASecondCallGivesTheSameSession()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);

        var first = await StartAsync(setup, order.Number);
        var second = await StartAsync(setup, order.Number);

        Assert.Equal(first, second);
        // The database keeps microseconds, so the deadline read back is a tick or two shorter.
        Assert.Equal(order.PaymentDueAt!.Value, first.ExpiresAt, TimeSpan.FromMilliseconds(1));
        var payment = Assert.Single(await PaymentsAsync(setup));
        Assert.Equal((3000L, "BGN", PaymentStatus.Pending), (payment.Amount, payment.Currency, payment.Status));
        Assert.Equal("test", payment.Provider);
        Assert.Equal(payment.RedirectUrl, first.RedirectUrl);
        Assert.Equal($"/pay/test/{payment.ProviderReference}", first.RedirectUrl);
    }

    [Fact]
    public async Task Start_ManyCallsAtTheSameMoment_LeaveOnePayment()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);

        var start = new TaskCompletionSource();
        var calls = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await StartAsync(setup, order.Number);
        })).ToList();
        start.SetResult();
        var sessions = await Task.WhenAll(calls);

        Assert.Single(sessions.Select(s => s.PaymentId).Distinct());
        Assert.Single(await PaymentsAsync(setup));
    }

    [Fact]
    public async Task Start_OfAnUnknownOrder_IsNotFound()
    {
        await using var setup = await CreateAsync();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => StartAsync(setup, 99999999));

        Assert.Equal("order.not_found", exception.Code);
    }

    [Fact]
    public async Task Start_WhenPaymentIsTurnedOff_IsNotFound()
    {
        await using var setup = await CreateAsync(paymentEnabled: false);
        var order = await PlaceAsync(setup);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => StartAsync(setup, order.Number));

        Assert.Equal("payment.disabled", exception.Code);
        Assert.Empty(await PaymentsAsync(setup));
    }

    [Fact]
    public async Task Start_OfAnOrderPastItsDeadline_IsNotPayable()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        setup.Time.Advance(TimeSpan.FromMinutes(60));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => StartAsync(setup, order.Number));

        Assert.Equal("payment.order_not_payable", exception.Code);
        Assert.Empty(await PaymentsAsync(setup));
    }

    [Fact]
    public async Task Start_OfAnOrderThatIsPaidOrCancelled_IsNotPayable()
    {
        await using var setup = await CreateAsync();
        var paid = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, paid);
        await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));
        var cancelled = await PlaceAsync(setup);
        await CancelAsync(setup, cancelled);

        var afterPaid = await Assert.ThrowsAsync<ConflictException>(() => StartAsync(setup, paid.Number));
        var afterCancel = await Assert.ThrowsAsync<ConflictException>(() => StartAsync(setup, cancelled.Number));

        Assert.Equal("payment.order_not_payable", afterPaid.Code);
        Assert.Equal("payment.order_not_payable", afterCancel.Code);
    }

    [Fact]
    public async Task Start_OfAnOrderWithoutADeadline_GivesASessionOfTheUsualLength()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        await using (var context = setup.Database.CreateContext())
        {
            await context.Orders.Where(o => o.Number == order.Number)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentDueAt, (DateTimeOffset?)null));
        }

        var session = await StartAsync(setup, order.Number);

        Assert.Equal(setup.Time.GetUtcNow().AddMinutes(60), session.ExpiresAt, TimeSpan.FromMilliseconds(1));
    }


    [Fact]
    public async Task Status_FollowsThePayment_AndNeverCarriesTheBuyer()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (session, reference) = await OpenAsync(setup, order);

        var pending = await InScopeAsync(setup, s => s.Payments.GetStatusAsync(session.PaymentId, default));
        await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));
        var paid = await InScopeAsync(setup, s => s.Payments.GetStatusAsync(session.PaymentId, default));

        Assert.Equal("Pending", pending.Status);
        Assert.Equal("Succeeded", paid.Status);
        Assert.Equal(["Status"], typeof(PaymentStatusDto).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task Status_OfAPaymentWhoseOrderWasCancelledBeforeTheAnswer_IsExpired()
    {
        await using var setup = await CreateAsync();
        var order = await PlaceAsync(setup);
        var (session, _) = await OpenAsync(setup, order);
        await CancelAsync(setup, order);

        var status = await InScopeAsync(setup, s => s.Payments.GetStatusAsync(session.PaymentId, default));

        Assert.Equal("Expired", status.Status);
    }

    [Fact]
    public async Task Status_OfAnUnknownPayment_IsNotFound()
    {
        await using var setup = await CreateAsync();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            InScopeAsync(setup, s => s.Payments.GetStatusAsync(Guid.NewGuid(), default)));

        Assert.Equal("payment.not_found", exception.Code);
    }
}
