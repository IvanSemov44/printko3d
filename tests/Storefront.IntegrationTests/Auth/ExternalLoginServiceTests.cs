using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Customers;
using Storefront.Domain.Orders;
using Storefront.Infrastructure.Identity;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

/// <summary>
/// Signing in through a provider (task 30d), with what a provider says made up here: no real provider is called. The sign-in cookie is
/// written to a made-up request.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExternalLoginServiceTests(PostgresFixture postgres)
{
    private const string Email = "buyer@example.test";
    private const string Password = "a made-up buyer password";

    private static ExternalIdentity Google(string key = "google-key-1", string? email = Email, bool verified = true, string? name = "Test Buyer") =>
        new("Google", key, email, verified, name);

    private static async Task<T> InScopeAsync<T>(AuthApiHost host, Func<ICustomerExternalLoginService, Task<T>> action)
    {
        await using var scope = host.Services.CreateAsyncScope();
        // The sign-in writes a cookie, so the service needs a request to write it to.
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return await action(scope.ServiceProvider.GetRequiredService<ICustomerExternalLoginService>());
    }

    private static Task<ExternalSignInResult> SignInAsync(AuthApiHost host, ExternalIdentity identity, Guid? linkTo = null) =>
        InScopeAsync(host, service => service.SignInAsync(identity, linkTo, default));

    private static async Task<CustomerUser> RegisterAsync(AuthApiHost host, bool confirmed, string email = Email)
    {
        await host.SendAsync(HttpMethod.Post, "/api/v1/account/register", new { email, password = Password, name = "Local" });
        await using var context = host.Database.CreateContext();
        var user = await context.CustomerUsers.AsNoTracking().SingleAsync(u => u.Email == email);
        if (confirmed)
        {
            await context.CustomerUsers.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, true));
        }

        return user;
    }

    private static async Task<int> CountAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        return await context.CustomerUsers.CountAsync();
    }

    [Fact]
    public async Task ANewVerifiedAddress_MakesAConfirmedAccountWithNoPassword_AndSignsItIn()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var result = await SignInAsync(host, Google());

        Assert.Equal(ExternalSignInOutcome.SignedIn, result.Outcome);
        var customer = result.Customer!.Customer;
        Assert.Equal(Email, customer.Email);
        Assert.Equal("Test Buyer", customer.DisplayName);
        Assert.True(customer.EmailConfirmed);
        Assert.False(customer.HasPassword);
        Assert.Equal(["Google"], customer.LinkedProviders);
        await using var context = host.Database.CreateContext();
        var link = await context.CustomerExternalLogins.SingleAsync();
        Assert.Equal(result.Customer.Id, link.CustomerId);
        Assert.Equal("google-key-1", link.ProviderKey);
    }

    [Fact]
    public async Task TheSameProviderAccount_SignsTheSameAccountInAgain_AndMakesNoSecond()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var first = await SignInAsync(host, Google());

        // The address at the provider has changed since; the account is found by the provider key.
        var second = await SignInAsync(host, Google(email: "changed@example.test"));

        Assert.Equal(first.Customer!.Id, second.Customer!.Id);
        Assert.Equal(1, await CountAsync(host));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("buyer@example.test", false)]
    public async Task AnAddressThatTheProviderDidNotVerify_MakesAndJoinsNothing(string? email, bool verified)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var result = await SignInAsync(host, Google(email: email, verified: verified));

        Assert.Equal(ExternalSignInOutcome.EmailNotVerified, result.Outcome);
        Assert.Null(result.Customer);
        Assert.Equal(0, await CountAsync(host));
        await using var context = host.Database.CreateContext();
        Assert.Equal(0, await context.CustomerExternalLogins.CountAsync());
    }

    [Fact]
    public async Task AConfirmedAccountOfTheSameAddress_IsNotJoinedByTheProvider()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host, confirmed: true);

        var result = await SignInAsync(host, Google());

        Assert.Equal(ExternalSignInOutcome.AccountExists, result.Outcome);
        Assert.Null(result.Customer);
        await using var context = host.Database.CreateContext();
        Assert.Equal(0, await context.CustomerExternalLogins.CountAsync());
        // The password of the account still works.
        var login = await host.SendAsync(HttpMethod.Post, "/api/v1/account/login", new { email = Email, password = Password });
        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task AnAccountThatWasNeverConfirmed_IsTakenOverByTheOwnerOfTheAddress_AndItsPasswordIsTakenAway()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var squatter = await RegisterAsync(host, confirmed: false);

        var result = await SignInAsync(host, Google());

        Assert.Equal(ExternalSignInOutcome.SignedIn, result.Outcome);
        Assert.Equal(squatter.Id, result.Customer!.Id);
        Assert.True(result.Customer.Customer.EmailConfirmed);
        Assert.False(result.Customer.Customer.HasPassword);
        await using var context = host.Database.CreateContext();
        var after = await context.CustomerUsers.AsNoTracking().SingleAsync();
        Assert.Null(after.PasswordHash);
        Assert.NotEqual(squatter.SecurityStamp, after.SecurityStamp);
        // The password that the first one chose no longer opens the account.
        var login = await host.SendAsync(HttpMethod.Post, "/api/v1/account/login", new { email = Email, password = Password });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task SigningInThroughAProvider_JoinsTheOrdersPlacedAsAGuestWithTheAddress()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var orderId = Guid.NewGuid();
        await using (var context = host.Database.CreateContext())
        {
            context.Add(new Order
            {
                Id = orderId,
                Number = 12345678,
                Currency = "BGN",
                TotalAmount = 100,
                ShippingMethod = "test",
                CustomerName = "Test",
                Email = "BUYER@example.test",
                Phone = "0000",
                ShippingStreet = "Street 1",
                ShippingCity = "City",
                ShippingPostalCode = "1000",
                ShippingCountry = "BG",
                TermsVersion = "1",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        var result = await SignInAsync(host, Google());

        await using var check = host.Database.CreateContext();
        Assert.Equal(result.Customer!.Id, (await check.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).CustomerId);
    }

    [Fact]
    public async Task ASignedInBuyer_CanConnectAProvider_OnceForEachProvider()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var buyer = await RegisterAsync(host, confirmed: true);

        var linked = await SignInAsync(host, Google(), buyer.Id);
        var again = await SignInAsync(host, Google(), buyer.Id);
        var another = await SignInAsync(host, Google(key: "google-key-2"), buyer.Id);
        var facebook = await SignInAsync(host, new ExternalIdentity("Facebook", "google-key-1", Email, true, "Test Buyer"), buyer.Id);

        Assert.Equal(ExternalSignInOutcome.Linked, linked.Outcome);
        Assert.Equal(ExternalSignInOutcome.Linked, again.Outcome);
        Assert.Equal(ExternalSignInOutcome.AlreadyLinked, another.Outcome);
        Assert.Equal(ExternalSignInOutcome.Linked, facebook.Outcome);
        Assert.Null(linked.Customer);
        await using var context = host.Database.CreateContext();
        Assert.Equal(2, await context.CustomerExternalLogins.CountAsync());
    }

    [Fact]
    public async Task AProviderAccountOfAnotherBuyer_IsNotConnectedToThisOne()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SignInAsync(host, Google());
        var other = await RegisterAsync(host, confirmed: true, email: "other@example.test");

        var result = await SignInAsync(host, Google(), other.Id);

        Assert.Equal(ExternalSignInOutcome.AlreadyLinked, result.Outcome);
        await using var context = host.Database.CreateContext();
        Assert.Equal(1, await context.CustomerExternalLogins.CountAsync());
    }

    [Fact]
    public async Task AccountOfAProvider_CanSetAPasswordThroughForgotPassword()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SignInAsync(host, Google());

        await host.SendAsync(HttpMethod.Post, "/api/v1/account/forgot-password", new { email = Email });
        await using var context = host.Database.CreateContext();
        var mail = await context.OutgoingEmails.AsNoTracking().SingleAsync(e => e.ToAddress == Email);
        var token = Uri.UnescapeDataString(System.Text.RegularExpressions.Regex.Match(mail.TextBody, @"token=([^\s]+)").Groups[1].Value);
        var reset = await host.SendAsync(HttpMethod.Post, "/api/v1/account/reset-password", new { email = Email, token, newPassword = "a new made-up password" });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, reset.StatusCode);
        var login = await host.SendAsync(HttpMethod.Post, "/api/v1/account/login", new { email = Email, password = "a new made-up password" });
        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
        var linkedProviders = (await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("linkedProviders");
        Assert.Equal("Google", Assert.Single(linkedProviders.EnumerateArray()).GetString());
    }
}
