using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.IntegrationTests.Payments;
using static Storefront.IntegrationTests.Payments.PaymentWorld;

namespace Storefront.IntegrationTests.Persistence;

/// <summary>Finding an order again without an account (task 27d) against a real PostgreSQL, with a clock the tests move.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderLookupDatabaseTests(PostgresFixture postgres)
{
    private const string Email = "buyer@example.com";

    private static Task<OrderDto> FindAsync(Setup setup, int number, string email) =>
        InScopeAsync(setup, s => s.OrderLookup.FindAsync(number, email, default));

    private static Task<OrderDto> FindByTokenAsync(Setup setup, string token) =>
        InScopeAsync(setup, s => s.OrderLookup.FindByTokenAsync(token, default));

    [Fact]
    public async Task TheNumberAndTheEmail_ShowTheOrderWithItsStatusAndLines()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);

        var found = await FindAsync(setup, order.Number, Email);

        Assert.Equal(order.Number, found.Number);
        Assert.Equal("New", found.Status);
        Assert.Equal(3000, found.Total);
        Assert.Single(found.Items);
    }

    [Theory]
    [InlineData("BUYER@EXAMPLE.COM")]
    [InlineData("  buyer@example.com  ")]
    public async Task TheEmail_IsComparedWithoutCapitalsAndSpaces(string typed)
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);

        var found = await FindAsync(setup, order.Number, typed);

        Assert.Equal(order.Number, found.Number);
    }

    [Fact]
    public async Task AWrongEmailAndAnUnknownNumber_GiveTheSameAnswer()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);

        var wrongEmail = await Assert.ThrowsAsync<NotFoundException>(() => FindAsync(setup, order.Number, "other@example.com"));
        var unknownNumber = await Assert.ThrowsAsync<NotFoundException>(() => FindAsync(setup, order.Number + 1, Email));

        Assert.Equal("order.not_found", wrongEmail.Code);
        Assert.Equal(wrongEmail.Code, unknownNumber.Code);
        Assert.Equal(wrongEmail.Message, unknownNumber.Message);
    }

    [Fact]
    public async Task AfterTooManyWrongEmails_TheNumberIsClosed_EvenForTheRightEmail()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<NotFoundException>(() => FindAsync(setup, order.Number, $"guess{attempt}@example.com"));
        }

        await Assert.ThrowsAsync<NotFoundException>(() => FindAsync(setup, order.Number, Email));
    }

    [Fact]
    public async Task ANumberThatWasClosed_OpensAgainAfterTheWindow()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<NotFoundException>(() => FindAsync(setup, order.Number, $"guess{attempt}@example.com"));
        }

        setup.Time.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal(order.Number, (await FindAsync(setup, order.Number, Email)).Number);
    }

    [Fact]
    public async Task FewWrongEmails_DoNotCloseTheNumber_AndRightOnesAreNotCounted()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await Assert.ThrowsAsync<NotFoundException>(() => FindAsync(setup, order.Number, $"guess{attempt}@example.com"));
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(order.Number, (await FindAsync(setup, order.Number, Email)).Number);
        }
    }

    [Fact]
    public async Task WrongEmailsForOneOrder_DoNotCloseAnotherOrder()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var first = await PlaceAsync(setup);
        var second = await PlaceAsync(setup);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<NotFoundException>(() => FindAsync(setup, first.Number, $"guess{attempt}@example.com"));
        }

        Assert.Equal(second.Number, (await FindAsync(setup, second.Number, Email)).Number);
    }

    [Theory]
    [InlineData(0, "buyer@example.com", "number")]
    [InlineData(12345678, "", "email")]
    [InlineData(12345678, "   ", "email")]
    public async Task AnEmptyField_Is400_AndNamesTheField(int number, string email, string field)
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() => FindAsync(setup, number, email));

        Assert.Contains(field, error.Errors.Keys);
    }

    [Fact]
    public async Task TheTokenFromTheEmail_ShowsTheOrder_AndFollowsItsStatus()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        var token = (await OrderAsync(setup, order.Number)).AccessToken!;

        var found = await FindByTokenAsync(setup, token);

        Assert.Equal(order.Number, found.Number);
        Assert.Equal("New", found.Status);
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task AWrongToken_IsNotFound(string token)
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        await PlaceAsync(setup);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => FindByTokenAsync(setup, token));

        Assert.Equal("order.not_found", error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task AnEmptyOrTooLongToken_Is400(string token)
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);

        var error = await Assert.ThrowsAsync<ValidationFailedException>(() => FindByTokenAsync(setup, token));

        Assert.Contains("token", error.Errors.Keys);
    }

    [Fact]
    public async Task AnOrderMadeBeforeTheTokenExisted_IsStillFoundByNumberAndEmail()
    {
        await using var setup = await PaymentWorld.CreateAsync(postgres);
        var order = await PlaceAsync(setup);
        await using (var context = setup.Database.CreateContext())
        {
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(
                context.Database, "UPDATE orders SET access_token = NULL");
        }

        // The order is still found by number and email; no token reaches it.
        Assert.Equal(order.Number, (await FindAsync(setup, order.Number, Email)).Number);
        await Assert.ThrowsAsync<NotFoundException>(() => FindByTokenAsync(setup, "null"));
    }
}
