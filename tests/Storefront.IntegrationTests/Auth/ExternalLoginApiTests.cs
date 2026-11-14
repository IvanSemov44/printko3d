using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

/// <summary>The addresses of the social sign-in (task 30d). No real provider is called: the tests stop at the redirect to it.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ExternalLoginApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/account/external";
    private const string CustomerCookieName = "__Host-printko-customer";
    private const string IntentCookieName = "__Host-printko-link-intent";
    private const string Shop = "http://localhost:5173";
    private const string Email = "buyer@example.test";
    private const string Password = "a made-up buyer password";

    // Made-up keys, for the tests only: they open nothing at a provider.
    private static readonly Dictionary<string, string> BothProviders = new()
    {
        ["Authentication:Google:ClientId"] = "test-google-client-id",
        ["Authentication:Google:ClientSecret"] = "test-google-client-secret",
        ["Authentication:Facebook:AppId"] = "test-facebook-app-id",
        ["Authentication:Facebook:AppSecret"] = "test-facebook-app-secret",
    };

    private static string? CookieNamed(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal))?.Split(';')[0]
            : null;

    private static async Task<string> SignedInAsync(AuthApiHost host)
    {
        await host.SendAsync(HttpMethod.Post, "/api/v1/account/register", new { email = Email, password = Password, name = "Buyer" });
        var response = await host.SendAsync(HttpMethod.Post, "/api/v1/account/login", new { email = Email, password = Password });
        return CookieNamed(response, CustomerCookieName)!;
    }

    private static Uri LocationOf(HttpResponseMessage response) =>
        response.Headers.Location ?? throw new InvalidOperationException("The answer has no Location.");

    [Fact]
    public async Task Providers_AreEmpty_WithNoKeys()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("providers").EnumerateArray());
    }

    [Fact]
    public async Task Providers_ListOnlyTheOnesWithBothKeys()
    {
        var onlyGoogle = new Dictionary<string, string>
        {
            ["Authentication:Google:ClientId"] = "test-google-client-id",
            ["Authentication:Google:ClientSecret"] = "test-google-client-secret",
            ["Authentication:Facebook:AppId"] = "test-facebook-app-id",
        };
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: onlyGoogle);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/providers");

        var providers = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("providers");
        Assert.Equal("Google", Assert.Single(providers.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Providers_ListBoth_WhenBothAreSet()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: BothProviders);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/providers");

        var providers = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("providers");
        Assert.Equal(new[] { "Google", "Facebook" }, providers.EnumerateArray().Select(p => p.GetString()!).ToArray());
    }

    [Theory]
    [InlineData("google")]
    [InlineData("facebook")]
    [InlineData("github")]
    public async Task Start_ForAProviderThatIsNotTurnedOn_Answers404(string provider)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/{provider}/start");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Start_SendsTheBrowserToGoogle_AndBackToTheAddressOfTheShop()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: BothProviders);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/google/start");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = LocationOf(response);
        Assert.Equal("accounts.google.com", location.Host);
        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("test-google-client-id", query["client_id"]);
        // The provider sends the buyer back to the shop, not to the internal address of the API.
        Assert.Equal($"{Shop}/signin-google", query["redirect_uri"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.Contains("email", query["scope"]);
        Assert.DoesNotContain("test-google-client-secret", location.ToString());
        // The cookie that ties the answer to this request.
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), v => v.Contains(".AspNetCore.Correlation.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Start_SendsTheBrowserToFacebook_AndBackToTheAddressOfTheShop()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: BothProviders);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/facebook/start");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = LocationOf(response);
        Assert.EndsWith("facebook.com", location.Host);
        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("test-facebook-app-id", query["client_id"]);
        Assert.Equal($"{Shop}/signin-facebook", query["redirect_uri"]);
        Assert.DoesNotContain("test-facebook-app-secret", location.ToString());
    }

    [Fact]
    public async Task Start_TakesTheAddressOfTheShopFromTheSettings_NotFromTheRequest()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: new Dictionary<string, string>(BothProviders)
        {
            ["Site:Url"] = "https://shop.example",
        });
        var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/google/start");
        request.Headers.Host = "evil.example";
        request.Headers.Add("X-Forwarded-Host", "evil.example");

        var response = await host.Client.SendAsync(request);

        var query = HttpUtility.ParseQueryString(LocationOf(response).Query);
        Assert.Equal("https://shop.example/signin-google", query["redirect_uri"]);
    }

    [Fact]
    public async Task Link_NeedsASession_AndAProviderThatIsTurnedOn()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: BothProviders);
        var session = await SignedInAsync(host);

        var anonymous = await host.SendAsync(HttpMethod.Post, $"{Base}/google/link");
        var unknown = await host.SendAsync(HttpMethod.Post, $"{Base}/github/link", cookie: session);
        var noHeader = await host.SendAsync(HttpMethod.Post, $"{Base}/google/link", cookie: session, csrfHeader: false);
        var granted = await host.SendAsync(HttpMethod.Post, $"{Base}/google/link", cookie: session);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
        Assert.NotNull(CookieNamed(granted, IntentCookieName));
    }

    [Fact]
    public async Task Start_ToConnect_NeedsTheSessionAndTheIntent()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: BothProviders);
        var session = await SignedInAsync(host);
        var intent = CookieNamed(await host.SendAsync(HttpMethod.Post, $"{Base}/google/link", cookie: session), IntentCookieName)!;
        var facebookIntent = CookieNamed(await host.SendAsync(HttpMethod.Post, $"{Base}/facebook/link", cookie: session), IntentCookieName)!;

        var withBoth = await host.SendAsync(HttpMethod.Get, $"{Base}/google/start?mode=link", cookie: $"{session}; {intent}");
        var withoutIntent = await host.SendAsync(HttpMethod.Get, $"{Base}/google/start?mode=link", cookie: session);
        var withoutSession = await host.SendAsync(HttpMethod.Get, $"{Base}/google/start?mode=link", cookie: intent);
        var otherProvider = await host.SendAsync(HttpMethod.Get, $"{Base}/google/start?mode=link", cookie: $"{session}; {facebookIntent}");

        Assert.Equal("accounts.google.com", LocationOf(withBoth).Host);
        foreach (var refused in new[] { withoutIntent, withoutSession, otherProvider })
        {
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal($"{Shop}/account/login?error=external_failed", LocationOf(refused).ToString());
        }
    }

    [Fact]
    public async Task Callback_WithNothingFromAProvider_SendsTheBuyerToTheSignInWithAnError()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: BothProviders);

        var response = await host.SendAsync(HttpMethod.Get, $"{Base}/callback?mode=signin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{Shop}/account/login?error=external_failed", LocationOf(response).ToString());
        Assert.Null(CookieNamed(response, CustomerCookieName));
    }

    [Fact]
    public async Task TheCallbackOfAProvider_WithAnAnswerThatIsNotRight_SignsNobodyIn()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres, extraSettings: BothProviders);

        var response = await host.SendAsync(HttpMethod.Get, "/signin-google?code=forged&state=forged");

        // The answer does not match a request that this browser made: no cookie of a sign-in, and no account.
        Assert.Null(CookieNamed(response, CustomerCookieName));
        await using var context = host.Database.CreateContext();
        Assert.Empty(context.CustomerUsers);
    }
}
