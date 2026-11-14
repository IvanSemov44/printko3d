using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed partial class ClosedByDefaultTests(PostgresFixture postgres)
{
    // The complete list of addresses that need no sign-in. A new address that is open must be added here on purpose.
    private static readonly string[] OpenAddresses =
    [
        "DELETE api/v1/cart",
        "DELETE api/v1/cart/items/{variantId:guid}",
        "GET api/v1/cart",
        "GET api/v1/cart/items/{variantId:guid}",
        "POST api/v1/cart/items",
        "PUT api/v1/cart/items/{variantId:guid}",
        // A buyer without an account places an order from the cart cookie (decision 0032, task 23b2).
        "POST api/v1/orders",
        // The same buyer sees the order again, by number and email or by the key of the link in an email (decision 0036, task 27d).
        "POST api/v1/orders/lookup",
        "POST api/v1/orders/lookup-by-token",
        "GET api/v1/shipping/quotes",
        // A buyer without an account pays an order; the provider calls the notifications address with a signature (decision 0035, task 26c2).
        "PUT api/v1/orders/{number:int}/payment",
        "GET api/v1/payments/mode",
        "GET api/v1/payments/{paymentId:guid}",
        "POST api/v1/payments/notifications",
        "POST api/v1/payments/test/{reference}/complete",
        "GET api/v1/categories",
        "GET api/v1/products",
        "GET api/v1/products/{slug}",
        "GET api/v1/store-settings",
        "GET health",
        "POST api/v1/auth/login",
        "GET openapi/{documentName}.json",
        "GET sitemap.xml",
    ];

    [Fact]
    public async Task EveryAddressOfTheApi_IsExplicitlyOpen_OrAnswers401WithoutASignIn()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => (
                Pattern: e.RoutePattern.RawText!.TrimStart('/'),
                Methods: e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
                Open: e.Metadata.GetMetadata<IAllowAnonymous>() is not null))
            .ToList();

        var open = endpoints.Where(e => e.Open)
            .SelectMany(e => e.Methods.Select(method => $"{method} {e.Pattern}"))
            .Order().ToList();
        Assert.Equal(OpenAddresses.Order().ToList(), open);

        var closed = endpoints.Where(e => !e.Open).ToList();
        Assert.NotEmpty(closed);
        foreach (var endpoint in closed)
        {
            foreach (var method in endpoint.Methods)
            {
                var url = "/" + RouteParameter().Replace(endpoint.Pattern, "00000000-0000-0000-0000-000000000001");
                var response = await host.SendAsync(new HttpMethod(method), url);
                Assert.True(
                    response.StatusCode == HttpStatusCode.Unauthorized,
                    $"{method} {endpoint.Pattern} answered {(int)response.StatusCode} without a sign-in.");
                Assert.True(response.IsProblemJson());
            }
        }
    }

    [Fact]
    public async Task AnAddressNobodyMarked_IsClosedByTheFallbackPolicy()
    {
        await using var host = await AuthApiHost.CreateAsync(
            postgres,
            configureServices: services => services.AddControllers().AddApplicationPart(typeof(UnmarkedController).Assembly));

        var anonymous = await host.SendAsync(HttpMethod.Get, "/test-only/unmarked");
        var signedIn = await host.SendAsync(HttpMethod.Get, "/test-only/unmarked", cookie: await host.LoginAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/categories")]
    [InlineData("/api/v1/products")]
    [InlineData("/health")]
    public async Task PublicAddresses_StillWorkWithoutASignIn(string url)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Get, url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}

/// <summary>Has no [Authorize] and no [AllowAnonymous] on purpose. Only the test above adds it to the API.</summary>
[ApiController]
[Route("test-only/unmarked")]
public sealed class UnmarkedController : ControllerBase
{
    [HttpGet]
    public string Get() => "reached";
}
