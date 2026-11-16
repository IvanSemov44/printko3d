using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Storefront.Domain.Emails;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

/// <summary>The emails of a buyer's account (task 30c): the confirmation of the address and the new password.</summary>
[Collection(PostgresCollection.Name)]
public sealed partial class AccountEmailsApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/account";
    private const string CookieName = "__Host-printko-customer";
    private const string Email = "buyer@example.test";
    private const string Password = "a made-up buyer password";
    private const string NewPassword = "another made-up password";

    [GeneratedRegex(@"token=(?<token>[^\s]+)")]
    private static partial Regex TokenInLink();

    private static string? CookieOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))?.Split(';')[0]
            : null;

    private static async Task<List<OutgoingEmail>> QueueAsync(AuthApiHost host, string to = Email)
    {
        await using var context = host.Database.CreateContext();
        return await context.OutgoingEmails.AsNoTracking()
            .Where(e => e.ToAddress == to)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }

    private static string TokenOf(OutgoingEmail email) =>
        Uri.UnescapeDataString(TokenInLink().Match(email.TextBody).Groups["token"].Value);

    private static Task<HttpResponseMessage> RegisterAsync(AuthApiHost host) =>
        host.SendAsync(HttpMethod.Post, $"{Base}/register", new { email = Email, password = Password, name = "Buyer" });

    private static Task<HttpResponseMessage> LoginAsync(AuthApiHost host, string password = Password) =>
        host.SendAsync(HttpMethod.Post, $"{Base}/login", new { email = Email, password });

    private static Task<HttpResponseMessage> ForgotAsync(AuthApiHost host, string email = Email) =>
        host.SendAsync(HttpMethod.Post, $"{Base}/forgot-password", new { email });

    private static Task<HttpResponseMessage> ResetAsync(AuthApiHost host, string token, string password = NewPassword) =>
        host.SendAsync(HttpMethod.Post, $"{Base}/reset-password", new { email = Email, token, newPassword = password });

    private static async Task<string> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

    // --- Confirmation of the address ---

    [Fact]
    public async Task Register_QueuesAConfirmationEmail_WithALinkToTheShop_AndTheAddressAndTheToken()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        await RegisterAsync(host);

        var email = Assert.Single(await QueueAsync(host));
        Assert.Contains("Потвърдете", email.Subject);
        Assert.Contains("/account/confirm-email?email=buyer%40example.test&token=", email.TextBody);
        Assert.Contains("120 минути", email.TextBody);
        Assert.DoesNotContain("{", email.TextBody);
        Assert.DoesNotContain("{", email.Subject);
        Assert.False(string.IsNullOrEmpty(TokenOf(email)));
    }

    [Fact]
    public async Task ConfirmEmail_WithTheTokenOfTheEmail_ConfirmsTheAddress()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        var token = TokenOf(Assert.Single(await QueueAsync(host)));

        var response = await host.SendAsync(HttpMethod.Post, $"{Base}/confirm-email", new { email = Email, token });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var context = host.Database.CreateContext();
        Assert.True((await context.CustomerUsers.SingleAsync()).EmailConfirmed);
    }

    [Theory]
    [InlineData("a wrong token")]
    [InlineData("")]
    public async Task ConfirmEmail_WithAWrongToken_Answers422OrValidation_AndConfirmsNothing(string token)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);

        var response = await host.SendAsync(HttpMethod.Post, $"{Base}/confirm-email", new { email = Email, token });

        Assert.True(response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest);
        await using var context = host.Database.CreateContext();
        Assert.False((await context.CustomerUsers.SingleAsync()).EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_WithAnotherAddress_OrAnUnknownOne_GivesTheSameRefusal()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        var token = TokenOf(Assert.Single(await QueueAsync(host)));

        var unknown = await host.SendAsync(HttpMethod.Post, $"{Base}/confirm-email", new { email = "nobody@example.test", token });
        var wrong = await host.SendAsync(HttpMethod.Post, $"{Base}/confirm-email", new { email = Email, token = "x" + token });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal("account.token_invalid", await ProblemCodeAsync(unknown));
        Assert.Equal("account.token_invalid", await ProblemCodeAsync(wrong));
    }

    [Fact]
    public async Task Register_ForAnAddressThatHasAnAccount_QueuesAnEmailToItsOwner_WithoutAToken()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);

        await RegisterAsync(host);

        var emails = await QueueAsync(host);
        Assert.Equal(2, emails.Count);
        Assert.Contains("Вече имате акаунт", emails[1].Subject);
        Assert.Contains("/account/login", emails[1].TextBody);
        Assert.Contains("/account/forgot-password", emails[1].TextBody);
        Assert.DoesNotContain("token=", emails[1].TextBody);
    }

    [Fact]
    public async Task ResendConfirmation_QueuesAnotherEmail_UntilTheAddressIsConfirmed()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        var cookie = CookieOf(await LoginAsync(host))!;

        var response = await host.SendAsync(HttpMethod.Post, $"{Base}/resend-confirmation", cookie: cookie);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var emails = await QueueAsync(host);
        Assert.Equal(2, emails.Count);
        await host.SendAsync(HttpMethod.Post, $"{Base}/confirm-email", new { email = Email, token = TokenOf(emails[1]) });

        await host.SendAsync(HttpMethod.Post, $"{Base}/resend-confirmation", cookie: cookie);

        Assert.Equal(2, (await QueueAsync(host)).Count);
    }

    [Fact]
    public async Task ResendConfirmation_WithoutASession_Answers401()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Post, $"{Base}/resend-confirmation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Forgotten password ---

    [Fact]
    public async Task ForgotPassword_QueuesAnEmailForAKnownAddress_AndNothingForAnUnknownOne_WithTheSameAnswer()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);

        var known = await ForgotAsync(host);
        var unknown = await ForgotAsync(host, "nobody@example.test");

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        var emails = await QueueAsync(host);
        Assert.Equal(2, emails.Count);
        Assert.Contains("Нова парола", emails[1].Subject);
        Assert.Contains("/account/reset-password?email=buyer%40example.test&token=", emails[1].TextBody);
        Assert.Empty(await QueueAsync(host, "nobody@example.test"));
    }

    [Fact]
    public async Task ResetPassword_SetsTheNewPassword_ConfirmsTheAddress_AndEndsEverySession()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        var session = CookieOf(await LoginAsync(host))!;
        await ForgotAsync(host);
        var token = TokenOf((await QueueAsync(host))[1]);

        var response = await ResetAsync(host, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(CookieOf(response));
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(host)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(host, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, $"{Base}/me", cookie: session)).StatusCode);
        await using var context = host.Database.CreateContext();
        Assert.True((await context.CustomerUsers.SingleAsync()).EmailConfirmed);
    }

    [Fact]
    public async Task ResetPassword_LinkWorksOnce()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        await ForgotAsync(host);
        var token = TokenOf((await QueueAsync(host))[1]);
        await ResetAsync(host, token);

        var again = await ResetAsync(host, token, "a third made-up password");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Equal("account.token_invalid", await ProblemCodeAsync(again));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(host, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithAWeakPassword_Answers400_AndKeepsTheLinkUsable()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        await ForgotAsync(host);
        var token = TokenOf((await QueueAsync(host))[1]);

        var weak = await ResetAsync(host, token, "short");
        var good = await ResetAsync(host, token);

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, good.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithAWrongTokenOrAnUnknownAddress_GivesTheSameRefusal()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);

        var wrong = await ResetAsync(host, "a wrong token");
        var unknown = await host.SendAsync(
            HttpMethod.Post, $"{Base}/reset-password", new { email = "nobody@example.test", token = "a wrong token", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal(await ProblemCodeAsync(wrong), await ProblemCodeAsync(unknown));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(host)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_LiftsTheLockoutOfAnAccount()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await RegisterAsync(host);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(host, "not the password at all");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(host)).StatusCode);
        await ForgotAsync(host);
        await ResetAsync(host, TokenOf((await QueueAsync(host))[1]));

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(host, NewPassword)).StatusCode);
    }
}
