using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Orders;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AdminOrdersApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/admin/orders";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task SeedAsync(AuthApiHost host, int number, OrderStatus status = OrderStatus.New)
    {
        await using var context = host.Database.CreateContext();
        context.Add(new Order
        {
            Number = number,
            Status = status,
            Currency = "BGN",
            TotalAmount = 3000,
            CustomerName = "Test Buyer",
            Email = "buyer@example.test",
            Phone = "+359000000",
            ShippingStreet = "1 Test Street",
            ShippingCity = "Sofia",
            ShippingPostalCode = "1000",
            ShippingCountry = "BG",
            TermsVersion = "draft-1",
            TermsAcceptedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    public static TheoryData<string, string> Addresses => new()
    {
        { "GET", Base },
        { "GET", Base + "/12345678" },
        { "POST", Base + "/12345678/status" },
        { "POST", Base + "/12345678/cancel" },
    };

    [Theory]
    [MemberData(nameof(Addresses))]
    public async Task EveryAddress_Answers401WithoutASignIn_AsProblemDetails(string method, string url)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(new HttpMethod(method), url, method == "GET" ? null : new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public async Task EveryAddress_Answers403WithoutTheAdminRole_AsProblemDetails(string method, string url)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await host.CreateUserWithoutRoleAsync("viewer@example.test", "another made-up password");
        var cookie = await host.LoginAsync("viewer@example.test", "another made-up password");

        var response = await host.SendAsync(new HttpMethod(method), url, method == "GET" ? null : new { }, cookie);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task TheList_IsAPage_WithoutThePhoneAndTheAddress()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, 12345678);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base + "?status=New&pageSize=5", cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        var item = body.GetProperty("items")[0];
        Assert.Equal(12345678, item.GetProperty("number").GetInt32());
        Assert.Equal("New", item.GetProperty("status").GetString());
        Assert.False(item.TryGetProperty("phone", out _));
        Assert.False(item.TryGetProperty("shippingStreet", out _));
    }

    [Theory]
    [InlineData("?status=Nonsense")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=0")]
    public async Task ABadQuery_Answers400_AsProblemDetails(string query)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base + query, cookie: cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task TheDetail_HasTheNextStepsAndWhetherTheOrderCanBeRefused()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, 12345678, OrderStatus.Paid);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base + "/12345678", cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal("buyer@example.test", body.GetProperty("order").GetProperty("email").GetString());
        Assert.Equal("InProduction", body.GetProperty("nextStatuses")[0].GetString());
        Assert.True(body.GetProperty("canCancel").GetBoolean());
    }

    [Fact]
    public async Task AnUnknownOrder_Answers404_WithTheCode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base + "/12345678", cookie: cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("order.not_found", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheStatusAddress_MovesOneStep_AndRefusesAJump_With409AndTheCode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, 12345678, OrderStatus.Paid);
        var cookie = await host.LoginAsync();

        var jump = await host.SendAsync(HttpMethod.Post, Base + "/12345678/status", new { status = "Shipped" }, cookie);
        var step = await host.SendAsync(HttpMethod.Post, Base + "/12345678/status", new { status = "InProduction" }, cookie);

        Assert.Equal(HttpStatusCode.Conflict, jump.StatusCode);
        Assert.Equal("order.status_transition_invalid", (await JsonOf(jump)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, step.StatusCode);
        Assert.Equal("InProduction", (await JsonOf(step)).GetProperty("order").GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheStatusAddress_WithAnUnknownStatus_Answers400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, 12345678, OrderStatus.Paid);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Post, Base + "/12345678/status", new { status = "Nonsense" }, cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task TheCancelAddress_RefusesAPaidOrder_AndSaysTheMoneyGoesBackByHand()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, 12345678, OrderStatus.Paid);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Post, Base + "/12345678/cancel", new { }, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal("Cancelled", body.GetProperty("order").GetProperty("order").GetProperty("status").GetString());
        Assert.True(body.GetProperty("refundByHand").GetBoolean());
        await using var context = host.Database.CreateContext();
        Assert.Equal(OrderStatus.Cancelled, (await context.Orders.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task TheCancelAddress_ForAShippedOrder_Answers409()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedAsync(host, 12345678, OrderStatus.Shipped);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Post, Base + "/12345678/cancel", new { }, cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order.status_transition_invalid", (await JsonOf(response)).GetProperty("code").GetString());
    }
}
