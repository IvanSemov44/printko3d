using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AccountApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/account";
    private const string CookieName = "__Host-printko-customer";

    // Made-up values for tests only.
    private const string Email = "buyer@example.test";
    private const string Password = "a made-up buyer password";

    private static string? CookieOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))?.Split(';')[0]
            : null;

    private static Task<HttpResponseMessage> RegisterAsync(AuthApiHost host, string email = Email, string password = Password) =>
        host.SendAsync(HttpMethod.Post, $"{Base}/register", new { email, password, name = "Buyer" });

    private static Task<HttpResponseMessage> LoginAsync(AuthApiHost host, string email = Email, string password = Password) =>
        host.SendAsync(HttpMethod.Post, $"{Base}/login", new { email, password });

    private static async Task<string> RegisterAndLoginAsync(AuthApiHost host)
    {
        Assert.Equal(HttpStatusCode.Accepted, (await RegisterAsync(host)).StatusCode);
        var response = await LoginAsync(host);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return CookieOf(response) ?? throw new InvalidOperationException("The sign-in set no cookie.");
    }

    [Fact]
    public async Task Register_MakesAnAccount_WithoutSigningIn()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await RegisterAsync(host);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(CookieOf(response));
        await using var context = host.Database.CreateContext();
        var user = await context.CustomerUsers.SingleAsync();
        Assert.Equal(Email, user.Email);
        Assert.Equal("Buyer", user.DisplayName);
        Assert.False(user.EmailConfirmed);
        Assert.NotEqual(Password, user.PasswordHash);
    }

    [Fact]
    public async Task Register_ForAnAddressThatHasAnAccount_AnswersTheSame_AndMakesNoSecondAccount()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);

        var again = await RegisterAsync(host, email: "BUYER@example.test", password: "another made-up password");

        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        await using var context = host.Database.CreateContext();
        Assert.Equal(1, await context.CustomerUsers.CountAsync());
        // The password of the account did not change.
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(host)).StatusCode);
    }

    [Fact]
    public async Task Register_WithAShortPassword_Answers400_AndMakesNoAccount()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await RegisterAsync(host, password: "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
        await using var context = host.Database.CreateContext();
        Assert.Equal(0, await context.CustomerUsers.CountAsync());
    }

    [Fact]
    public async Task Register_WithABadEmail_Answers400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await RegisterAsync(host, email: "not an email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutTheCsrfHeader_Answers403()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/register", new { email = Email, password = Password, name = "Buyer" }, csrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_SetsTheCookie_AndMeAnswersWithTheBuyer()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await RegisterAndLoginAsync(host);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/me", cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Email, me.GetProperty("email").GetString());
        Assert.Equal("Buyer", me.GetProperty("displayName").GetString());
        Assert.True(me.GetProperty("hasPassword").GetBoolean());
        Assert.False(me.GetProperty("emailConfirmed").GetBoolean());
    }

    [Fact]
    public async Task Login_Cookie_IsHttpOnlySecureAndLax()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);

        var response = await LoginAsync(host);

        var header = response.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_GiveTheSameAnswer()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);

        var wrongPassword = await LoginAsync(host, password: "not the password at all");
        var unknown = await LoginAsync(host, email: "nobody@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var wrong = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var missing = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var field in new[] { "code", "status", "title", "detail" })
        {
            Assert.Equal(
                wrong.TryGetProperty(field, out var a) ? a.ToString() : null,
                missing.TryGetProperty(field, out var b) ? b.ToString() : null);
        }

        Assert.Null(CookieOf(wrongPassword));
    }

    [Fact]
    public async Task Login_LocksTheAccountAfterFiveWrongPasswords_EvenForTheRightOne()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(host, password: "not the password at all");
        }

        var response = await LoginAsync(host);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutACookie_Answers401_AsProblemDetails()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task Logout_EndsTheSessionOnTheServer_EvenForACopyOfTheCookie()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await RegisterAndLoginAsync(host);

        var logout = await host.SendAsync(HttpMethod.Post, $"{Base}/logout", cookie: cookie);
        var afterwards = await host.SendAsync(HttpMethod.Get, $"{Base}/me", cookie: cookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task TheBuyersCookie_DoesNotOpenAdminAddresses()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await RegisterAndLoginAsync(host);

        var response = await host.SendAsync(HttpMethod.Get, "/api/v1/admin/categories", cookie: cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheAdministratorsCookie_DoesNotOpenTheBuyersAddresses()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var admin = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/me", cookie: admin);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithAWrongCurrentPassword_Answers422()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await RegisterAndLoginAsync(host);

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/change-password", new { currentPassword = "wrong wrong wrong", newPassword = "a new made-up password" }, cookie);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("account.current_password_wrong", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangePassword_SetsTheNewPassword_AndKeepsThisSession()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await RegisterAndLoginAsync(host);

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/change-password", new { currentPassword = Password, newPassword = "a new made-up password" }, cookie);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(host)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(host, password: "a new made-up password")).StatusCode);
        var renewed = CookieOf(response);
        Assert.NotNull(renewed);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(HttpMethod.Get, $"{Base}/me", cookie: renewed)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithAShortNewPassword_Answers400()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await RegisterAndLoginAsync(host);

        var response = await host.SendAsync(
            HttpMethod.Post, $"{Base}/change-password", new { currentPassword = Password, newPassword = "short" }, cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
