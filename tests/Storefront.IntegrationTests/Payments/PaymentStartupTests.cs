using System.Net;
using Storefront.Application.Payments;
using Storefront.IntegrationTests.Auth;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Payments;

[Collection(PostgresCollection.Name)]
public sealed class PaymentStartupTests(PostgresFixture postgres)
{
    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task TheApi_RefusesToStart_WithTheTestPaymentOutsideDevelopmentAndDemo(string environment)
    {
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var host = await AuthApiHost.CreateAsync(
                postgres, environment, createAdmin: false, loginEnabled: false);
        });

        Assert.NotNull(exception);
        Assert.Contains(Chain(exception), e => e is PaymentTestRefusedException);
    }

    [Fact]
    public async Task TheApi_StartsInProduction_WhenPaymentIsTurnedOff()
    {
        await using var host = await AuthApiHost.CreateAsync(
            postgres,
            "Production",
            createAdmin: false,
            loginEnabled: false,
            extraSettings: new Dictionary<string, string> { ["Payment:Enabled"] = "false" });

        var response = await host.SendAsync(HttpMethod.Get, "/api/v1/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Demo")]
    public async Task TheApi_StartsInDevelopmentAndDemo_WithTheTestPayment(string environment)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, environment, createAdmin: false);

        var response = await host.SendAsync(HttpMethod.Get, "/api/v1/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task TheApi_RefusesToStart_WithAWindowThatIsNotPositive(string minutes)
    {
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var host = await AuthApiHost.CreateAsync(
                postgres,
                "Development",
                createAdmin: false,
                extraSettings: new Dictionary<string, string> { ["Payment:WindowMinutes"] = minutes });
            await host.SendAsync(HttpMethod.Get, "/api/v1/categories");
        });

        Assert.NotNull(exception);
    }
}
