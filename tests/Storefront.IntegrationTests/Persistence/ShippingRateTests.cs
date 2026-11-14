using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Storefront.Application.Exceptions;
using Storefront.Application.Shipping;
using Storefront.Domain.Shipping;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class ShippingRateTests(PostgresFixture postgres)
{
    private static ShippingRate Step(int maxWeight, long price, string method = ShippingMethods.Address) =>
        new() { MethodCode = method, MaxWeightGrams = maxWeight, Price = price };

    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        var postgresException = exception as PostgresException ?? exception?.InnerException as PostgresException;
        return postgresException?.SqlState;
    }

    // The provider is taken from the real container, as the API does, so its registration is covered too.
    private static async Task<IReadOnlyList<ShippingQuote>> QuotesAsync(
        TestDatabase database,
        int weightGrams,
        params ShippingRate[] steps)
    {
        await using (var context = database.CreateContext())
        {
            context.AddRange(steps);
            await context.SaveChangesAsync();
        }

        await using var factory = new ApiFactory(database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IShippingProvider>()
            .GetQuotesAsync(new ShippingRequest(weightGrams, "BG", "Sofia", "1000"), default);
    }

    [Fact]
    public async Task Tariff_RejectsADuplicateStep_AndAcceptsTheSameWeightForAnotherMethod()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using (var context = database.CreateContext())
        {
            context.Add(Step(1000, 600));
            await context.SaveChangesAsync();
        }

        await using var duplicate = database.CreateContext();
        duplicate.Add(Step(1000, 700));
        Assert.Equal("23505", await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var other = database.CreateContext();
        other.Add(Step(1000, 700, method: "office"));
        await other.SaveChangesAsync();
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(-1, 600)]
    [InlineData(1000, -1)]
    public async Task Tariff_RejectsANonPositiveWeightAndANegativePrice(int weight, long price)
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(Step(weight, price));

        Assert.Equal("23514", await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Tariff_AcceptsAFreeStep()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(Step(1000, 0));

        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(1, 600)]
    [InlineData(1000, 600)]
    [InlineData(1001, 900)]
    [InlineData(5000, 900)]
    [InlineData(5001, 1500)]
    [InlineData(20000, 1500)]
    public async Task Provider_PicksTheFirstStepThatCoversTheWeight(int weight, long expected)
    {
        var database = await postgres.CreateDatabaseAsync();

        // The steps are written out of order on purpose: the order of reading must not decide the price.
        var quotes = await QuotesAsync(database, weight, Step(20000, 1500), Step(1000, 600), Step(5000, 900));

        var quote = Assert.Single(quotes);
        Assert.Equal(ShippingMethods.Address, quote.Code);
        Assert.Equal(expected, quote.Amount);
    }

    [Fact]
    public async Task Provider_RefusesAParcelHeavierThanTheLastStep_InsteadOfInventingAPrice()
    {
        var database = await postgres.CreateDatabaseAsync();

        var exception = await Assert.ThrowsAsync<RuleViolationException>(
            () => QuotesAsync(database, 5001, Step(1000, 600), Step(5000, 900)));

        Assert.Equal("shipping.too_heavy", exception.Code);
    }

    [Fact]
    public async Task Provider_OffersNothing_WhenNoTariffIsSetUp()
    {
        var database = await postgres.CreateDatabaseAsync();

        Assert.Empty(await QuotesAsync(database, 100));
    }

    [Fact]
    public async Task Provider_IgnoresTheStepsOfOtherMethods()
    {
        var database = await postgres.CreateDatabaseAsync();

        Assert.Empty(await QuotesAsync(database, 100, Step(1000, 300, method: "office")));
    }
}
