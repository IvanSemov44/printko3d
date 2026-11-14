using Microsoft.EntityFrameworkCore;
using Storefront.Application.Exceptions;
using Storefront.Application.Payments;
using Storefront.Domain.Emails;
using Storefront.Domain.Orders;
using Storefront.IntegrationTests.Payments;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Emails;

/// <summary>The emails of an order (task 27c): they are queued in the transaction of the change that causes them.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderEmailsDatabaseTests(PostgresFixture postgres)
{
    private static async Task<List<OutgoingEmail>> QueueAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.OutgoingEmails.AsNoTracking().OrderBy(e => e.CreatedAt).ToListAsync();
    }

    private static async Task PayAsync(Setup setup, Storefront.Application.Orders.OrderDto order)
    {
        var (_, reference) = await OpenAsync(setup, order);
        await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));
    }

    [Fact]
    public async Task AConfirmedOrder_QueuesOneEmailToTheBuyer_WithTheNumberTheTotalAndTheLink()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);

        var order = await PlaceAsync(setup);

        var email = Assert.Single(await QueueAsync(setup));
        var stored = await OrderAsync(setup, order.Number);
        Assert.Equal("buyer@example.com", email.ToAddress);
        Assert.Contains(order.Number.ToString(), email.Subject);
        Assert.Contains("30.00 BGN", email.TextBody);
        Assert.Contains("× 2", email.TextBody);
        Assert.False(string.IsNullOrEmpty(stored.AccessToken));
        Assert.Contains($"/my-order?token={stored.AccessToken}", email.TextBody);
        Assert.Null(email.HtmlBody);
        Assert.DoesNotContain("{", email.TextBody);
        Assert.DoesNotContain("{", email.Subject);
    }

    [Fact]
    public async Task EveryOrder_HasItsOwnToken()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);

        var first = await PlaceAsync(setup);
        var second = await PlaceAsync(setup);

        var tokens = new[] { (await OrderAsync(setup, first.Number)).AccessToken, (await OrderAsync(setup, second.Number)).AccessToken };
        Assert.Equal(2, tokens.Distinct().Count());
        Assert.All(tokens, token => Assert.True(token!.Length >= 43));
    }

    [Fact]
    public async Task AnOrderThatFails_QueuesNoEmail()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        string? token = null;
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(token, FirstVariantId, 2, default));
        token = change.Token;

        // The total the buyer saw is not the total of the cart, so the order is refused.
        await Assert.ThrowsAsync<ConflictException>(() =>
            InScopeAsync(setup, s => s.Orders.PlaceAsync(token, Input(1), default)));

        Assert.Empty(await QueueAsync(setup));
    }

    [Fact]
    public async Task APaidOrder_QueuesAnEmailOnce_EvenWhenTheMessageComesTwice()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        var message = await MessageAsync(setup, reference, PaymentOutcome.Paid, eventId: "evt-1");

        await SendAsync(setup, message);
        await SendAsync(setup, message);

        var emails = await QueueAsync(setup);
        Assert.Equal(2, emails.Count);
        var paid = emails[1];
        Assert.Contains("Платихте", paid.Subject);
        Assert.Contains("30.00 BGN", paid.TextBody);
        Assert.Contains("/my-order?token=", paid.TextBody);
    }

    [Fact]
    public async Task AFailedPayment_QueuesNoEmail()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);

        await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Failed));

        Assert.Single(await QueueAsync(setup));
    }

    [Fact]
    public async Task ALatePayment_ThatBringsTheOrderBack_QueuesThePaidEmail()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        await CancelAsync(setup, order);

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));

        Assert.Equal(PaymentNotificationResult.Revived, result);
        Assert.Equal(2, (await QueueAsync(setup)).Count);
    }

    [Fact]
    public async Task ALatePayment_ForAnOrderWhoseStockIsGone_QueuesNoPaidEmail()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres, firstStock: 2);
        var order = await PlaceAsync(setup);
        var (_, reference) = await OpenAsync(setup, order);
        await CancelAsync(setup, order);
        await using (var context = setup.Database.CreateContext())
        {
            await context.ProductVariants.Where(v => v.Id == FirstVariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, 0));
        }

        var result = await SendAsync(setup, await MessageAsync(setup, reference, PaymentOutcome.Paid));

        Assert.Equal(PaymentNotificationResult.PaidWithoutOrder, result);
        Assert.Single(await QueueAsync(setup));
    }

    [Fact]
    public async Task AShippedOrder_QueuesTheShippedEmail_WithTheAddress()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        await PayAsync(setup, order);

        await InScopeAsync(setup, s => s.OrderStatuses.ChangeAsync(order.Number, OrderStatus.InProduction, default));
        await InScopeAsync(setup, s => s.OrderStatuses.ChangeAsync(order.Number, OrderStatus.ReadyToShip, default));
        Assert.Equal(2, (await QueueAsync(setup)).Count);
        await InScopeAsync(setup, s => s.OrderStatuses.ChangeAsync(order.Number, OrderStatus.Shipped, default));

        var emails = await QueueAsync(setup);
        Assert.Equal(3, emails.Count);
        Assert.Contains("изпратена", emails[2].Subject);
        Assert.Contains("1 Test Street, 1000 Sofia", emails[2].TextBody);
    }

    [Fact]
    public async Task AMoveThatIsRefused_QueuesNoEmail()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);

        await Assert.ThrowsAsync<ConflictException>(() =>
            InScopeAsync(setup, s => s.OrderStatuses.ChangeAsync(order.Number, OrderStatus.Shipped, default)));

        Assert.Single(await QueueAsync(setup));
    }

    [Fact]
    public async Task TheAccessToken_IsUniqueInTheDatabase()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var first = await PlaceAsync(setup);
        var second = await PlaceAsync(setup);
        var token = (await OrderAsync(setup, first.Number)).AccessToken;

        await using var context = setup.Database.CreateContext();
        var error = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE orders SET access_token = {0} WHERE number = {1}", token!, second.Number));

        Assert.Contains("IX_orders_access_token", error.ToString());
    }
}
