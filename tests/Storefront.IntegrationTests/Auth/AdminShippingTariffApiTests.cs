using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Shipping;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AdminShippingTariffApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/admin/shipping-tariff";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static object Steps(params (int Weight, long Price)[] steps) => new
    {
        steps = steps.Select(s => new { maxWeightGrams = s.Weight, price = s.Price }).ToArray(),
    };

    private static async Task<List<(int Weight, long Price)>> StoredAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        return (await context.ShippingRates.AsNoTracking().OrderBy(r => r.MaxWeightGrams).ToListAsync())
            .Select(r => (r.MaxWeightGrams, r.Price)).ToList();
    }

    private static async Task SeedAsync(AuthApiHost host, params (int Weight, long Price)[] steps)
    {
        await using var context = host.Database.CreateContext();
        context.AddRange(steps.Select(s => new ShippingRate { MethodCode = "address", MaxWeightGrams = s.Weight, Price = s.Price }));
        await context.SaveChangesAsync();
    }

    public static TheoryData<string> Methods => new() { "GET", "PUT" };

    [Theory]
    [MemberData(nameof(Methods))]
    public async Task EveryAddress_Answers401WithoutASignIn_AsProblemDetails(string method)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(new HttpMethod(method), Base, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Theory]
    [MemberData(nameof(Methods))]
    public async Task EveryAddress_Answers403WithoutTheAdminRole_AsProblemDetails(string method)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await host.CreateUserWithoutRoleAsync("viewer@example.test", "another made-up password");
        var cookie = await host.LoginAsync("viewer@example.test", "another made-up password");

        var response = await host.SendAsync(new HttpMethod(method), Base, new { }, cookie);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task Get_ListsTheStepsFromTheLightestToTheHeaviest()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, (5000, 900), (1000, 600));
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base, cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var steps = (await JsonOf(response)).GetProperty("steps").EnumerateArray()
            .Select(s => (s.GetProperty("maxWeightGrams").GetInt32(), s.GetProperty("price").GetInt64())).ToList();
        Assert.Equal([(1000, 600L), (5000, 900L)], steps);
    }

    [Fact]
    public async Task Get_WithNoTariff_IsAnEmptyList()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base, cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await JsonOf(response)).GetProperty("steps").EnumerateArray());
    }

    [Fact]
    public async Task Put_ReplacesTheWholeTariff_AnswersSortedByWeight_AndTheBuyerSeesTheNewPrice()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, (1000, 600), (5000, 900));
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Put, Base, Steps((8000, 1500), (500, 400)), cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var steps = (await JsonOf(response)).GetProperty("steps").EnumerateArray()
            .Select(s => (s.GetProperty("maxWeightGrams").GetInt32(), s.GetProperty("price").GetInt64())).ToList();
        Assert.Equal([(500, 400L), (8000, 1500L)], steps);
        Assert.Equal([(500, 400L), (8000, 1500L)], await StoredAsync(host));
    }

    [Fact]
    public async Task Put_WithAnEmptyList_SwitchesTheDeliveryOff()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, (1000, 600));
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Put, Base, Steps(), cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await StoredAsync(host));
    }

    [Fact]
    public async Task Put_WithAFreeStep_IsAccepted()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Put, Base, Steps((1000, 0)), cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([(1000, 0L)], await StoredAsync(host));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-5, 100)]
    [InlineData(1_000_001, 100)]
    [InlineData(1000, -1)]
    [InlineData(1000, 100_000_001)]
    public async Task Put_WithAWeightOrAPriceOutOfRange_Is400_AndKeepsTheOldTariff(int weight, long price)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, (1000, 600));
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Put, Base, Steps((weight, price)), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([(1000, 600L)], await StoredAsync(host));
    }

    [Fact]
    public async Task Put_WithTwoStepsOfTheSameWeight_Is400_AndKeepsTheOldTariff()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, (1000, 600));
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Put, Base, Steps((2000, 700), (2000, 800)), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.failed", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Equal([(1000, 600L)], await StoredAsync(host));
    }

    [Fact]
    public async Task Put_WithMoreThanTwentySteps_Is400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();
        var steps = Enumerable.Range(1, 21).Select(i => (i * 100, (long)i)).ToArray();

        var response = await host.SendAsync(HttpMethod.Put, Base, Steps(steps), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await StoredAsync(host));
    }

    [Fact]
    public async Task Put_WithoutTheProtectiveHeader_Is403_AndChangesNothing()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, (1000, 600));
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Put, Base, Steps((2000, 700)), cookie, csrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([(1000, 600L)], await StoredAsync(host));
    }
}
