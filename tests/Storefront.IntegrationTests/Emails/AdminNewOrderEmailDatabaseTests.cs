using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Emails;
using Storefront.IntegrationTests.Persistence;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Emails;

/// <summary>The email to the administrator about a new order (task 28c): queued in the transaction of the order.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AdminNewOrderEmailDatabaseTests(PostgresFixture postgres)
{
    private const string BuyerEmail = "buyer@example.com";

    // CatalogData.Settings() has this contact email.
    private const string ShopEmail = "shop@example.com";

    private static async Task<List<OutgoingEmail>> QueueAsync(Setup setup)
    {
        await using var context = setup.Database.CreateContext();
        return await context.OutgoingEmails.AsNoTracking().OrderBy(e => e.CreatedAt).ToListAsync();
    }

    [Fact]
    public async Task ANewOrder_QueuesOneEmailToTheContactAddress_WithTheNumberTheSumTheLinesAndTheLink()
    {
        await using var setup = await CreateAsync(postgres);

        var order = await PlaceAsync(setup, withSecond: true);

        var email = Assert.Single(await QueueAsync(setup), e => e.ToAddress == ShopEmail);
        Assert.Contains(order.Number.ToString(), email.Subject);
        Assert.Contains("45.00 BGN", email.TextBody);
        Assert.Contains("2", email.TextBody);
        Assert.Contains($"http://localhost:5174/orders/{order.Number}", email.TextBody);
        Assert.Null(email.HtmlBody);
        Assert.DoesNotContain("{", email.TextBody);
        Assert.DoesNotContain("{", email.Subject);
    }

    [Fact]
    public async Task TheEmailToTheAdministrator_HoldsNothingThatTheBuyerTyped()
    {
        await using var setup = await CreateAsync(postgres);

        await PlaceAsync(setup);

        var email = Assert.Single(await QueueAsync(setup), e => e.ToAddress == ShopEmail);
        var text = email.Subject + email.TextBody;
        Assert.DoesNotContain("Test Buyer", text);
        Assert.DoesNotContain(BuyerEmail, text);
        Assert.DoesNotContain("+359 88 000 0000", text);
        Assert.DoesNotContain("1 Test Street", text);
        Assert.DoesNotContain("Sofia", text);
    }

    [Fact]
    public async Task TheLinkOfThePanel_IsASetting()
    {
        await using var setup = await CreateAsync(
            postgres, settings: new Dictionary<string, string> { ["Checkout:AdminUrl"] = "https://panel.example/" });

        var order = await PlaceAsync(setup);

        var email = Assert.Single(await QueueAsync(setup), e => e.ToAddress == ShopEmail);
        Assert.Contains($"https://panel.example/orders/{order.Number}", email.TextBody);
    }

    [Fact]
    public async Task WithNoContactAddress_NoEmailToTheAdministratorIsQueued_ButTheOrderAndTheBuyersEmailAre()
    {
        await using var setup = await CreateAsync(postgres);
        await using (var context = setup.Database.CreateContext())
        {
            await context.StoreSettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.ContactEmail, string.Empty));
        }

        var order = await PlaceAsync(setup);

        var queue = await QueueAsync(setup);
        var email = Assert.Single(queue);
        Assert.Equal(BuyerEmail, email.ToAddress);
        Assert.Equal(order.Number, (await OrderAsync(setup, order.Number)).Number);
    }

    [Fact]
    public async Task AnOrderThatFails_QueuesNoEmailToTheAdministrator()
    {
        await using var setup = await CreateAsync(postgres);
        var change = await InScopeAsync(setup, s => s.Cart.AddItemAsync(null, FirstVariantId, 2, default));
        await using (var context = setup.Database.CreateContext())
        {
            // Someone else took the pieces between the cart and the order.
            await context.ProductVariants.Where(v => v.Id == FirstVariantId).ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, 1));
        }

        await Assert.ThrowsAnyAsync<Exception>(
            () => InScopeAsync(setup, s => s.Orders.PlaceAsync(change.Token, Input(3000), default)));

        Assert.Empty(await QueueAsync(setup));
    }
}
