using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Auth;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AdminLoginTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Login_WithTheRightCredentials_ReturnsTheUserAndASessionCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.LoginRawAsync(AuthApiHost.AdminEmail, AuthApiHost.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(AuthApiHost.AdminEmail, user.GetProperty("email").GetString());
        Assert.NotNull(AuthApiHost.CookieOf(response));
    }

    [Fact]
    public async Task Login_SessionCookie_IsHttpOnlySecureAndSameSiteStrict()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.LoginRawAsync(AuthApiHost.AdminEmail, AuthApiHost.AdminPassword);

        var header = AuthApiHost.SetCookieHeaderOf(response)!.ToLowerInvariant();
        Assert.Contains("httponly", header, StringComparison.Ordinal);
        Assert.Contains("secure", header, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", header, StringComparison.Ordinal);
        Assert.Contains("path=/", header, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", header, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_GiveTheSameAnswer()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var wrongPassword = await host.LoginRawAsync(AuthApiHost.AdminEmail, "not the right password");
        var unknownEmail = await host.LoginRawAsync("nobody@example.test", AuthApiHost.AdminPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.True(wrongPassword.IsProblemJson());
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownEmail.Content.ReadAsStringAsync());
        Assert.Null(AuthApiHost.CookieOf(wrongPassword));
    }

    [Fact]
    public async Task Login_AfterTooManyWrongPasswords_LocksTheAccountEvenForTheRightPassword()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, attemptsPerMinute: 100);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.LoginRawAsync(AuthApiHost.AdminEmail, "wrong")).StatusCode);
        }

        var response = await host.LoginRawAsync(AuthApiHost.AdminEmail, AuthApiHost.AdminPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(AuthApiHost.CookieOf(response));
    }

    [Fact]
    public async Task Login_AfterTooManyRequestsFromOneClient_Returns429AsProblemDetails()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, attemptsPerMinute: 3);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.LoginRawAsync(AuthApiHost.AdminEmail, "wrong")).StatusCode);
        }

        var response = await host.LoginRawAsync(AuthApiHost.AdminEmail, AuthApiHost.AdminPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task Login_WithoutTheCsrfHeader_IsRefused()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email = AuthApiHost.AdminEmail, password = AuthApiHost.AdminPassword },
            csrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Null(AuthApiHost.CookieOf(response));
    }

    [Fact]
    public async Task Login_WithAnEmptyBody_Returns400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTheSessionCookie_ReturnsTheUser_AndWithoutItReturns401()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var signedIn = await host.SendAsync(HttpMethod.Get, "/api/v1/auth/me", cookie: cookie);
        var anonymous = await host.SendAsync(HttpMethod.Get, "/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(
            AuthApiHost.AdminEmail,
            (await signedIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSessionOnTheServer_EvenForAKeptCopyOfTheCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var logout = await host.SendAsync(HttpMethod.Post, "/api/v1/auth/logout", cookie: cookie);
        var reused = await host.SendAsync(HttpMethod.Get, "/api/v1/auth/me", cookie: cookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Session_SurvivesARestartOfTheApi_BecauseTheKeysAreInTheDatabase()
    {
        var database = await postgres.CreateDatabaseAsync();
        string cookie;
        await using (var first = await AuthApiHost.CreateAsync(postgres, database: database))
        {
            cookie = await first.LoginAsync();
        }

        await using var second = await AuthApiHost.CreateAsync(postgres, database: database, createAdmin: false);
        var response = await second.SendAsync(HttpMethod.Get, "/api/v1/auth/me", cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = database.CreateContext();
        Assert.True(await context.DataProtectionKeys.AnyAsync());
    }

    [Fact]
    public async Task AnAccountWithoutTheAdminRole_IsSignedInButForbiddenFromClosedAddresses()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await host.CreateUserWithoutRoleAsync("viewer@example.test", "another made-up password");
        var cookie = await host.LoginAsync("viewer@example.test", "another made-up password");

        var response = await host.SendAsync(HttpMethod.Get, "/api/v1/auth/me", cookie: cookie);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task ErrorAnswers_AreProblemDetailsWithoutInternalDetails()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Get, "/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task TheApi_RefusesToStart_WithAdminLoginEnabledOutsideDevelopmentAndDemo(string environment)
    {
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var host = await AuthApiHost.CreateAsync(postgres, environment, createAdmin: false);
        });

        Assert.NotNull(exception);
        Assert.Contains(Chain(exception), e => e is AdminLoginRefusedException);
    }

    [Fact]
    public async Task TheApi_StartsInProduction_WhenAdminLoginIsTurnedOff_AndTheLoginAnswers404()
    {
        await using var host = await AuthApiHost.CreateAsync(
            postgres, "Production", createAdmin: false, loginEnabled: false);

        var login = await host.LoginRawAsync(AuthApiHost.AdminEmail, AuthApiHost.AdminPassword);
        var publicList = await host.SendAsync(HttpMethod.Get, "/api/v1/categories");

        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
    }

    [Fact]
    public async Task TheApi_StartsInDemo()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, "Demo");

        var response = await host.LoginRawAsync(AuthApiHost.AdminEmail, AuthApiHost.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        yield return exception;
        IEnumerable<Exception> inner = exception is AggregateException aggregate
            ? aggregate.InnerExceptions
            : exception.InnerException is null ? [] : [exception.InnerException];
        foreach (var child in inner.SelectMany(Chain))
        {
            yield return child;
        }
    }
}
