using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AdminStoreSettingsApiTests(PostgresFixture postgres)
{
    private const string Base = "/api/v1/admin/store-settings";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task SeedSettingsAsync(AuthApiHost host)
    {
        await using var context = host.Database.CreateContext();
        context.StoreSettings.Add(CatalogData.Settings());
        await context.SaveChangesAsync();
    }

    private static object Body(
        uint version,
        string name = "New shop",
        string currency = "EUR",
        string email = "info@example.com",
        string phone = "+359 88 123 456",
        string color = "#2F5D8A",
        bool isDemo = true) =>
        new
        {
            storeName = name,
            defaultCurrency = currency,
            defaultLocale = "bg",
            contactEmail = email,
            contactPhone = phone,
            primaryColor = color,
            isDemo,
            version,
        };

    private static async Task<uint> VersionOfAsync(AuthApiHost host, string cookie) =>
        (await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie))).GetProperty("version").GetUInt32();

    // --- Who may call ---

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

    // --- Read ---

    [Fact]
    public async Task Get_ReturnsTheSettingsWithAVersion()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedSettingsAsync(host);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base, cookie: cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await JsonOf(response);
        Assert.Equal("Printko 3D", settings.GetProperty("storeName").GetString());
        Assert.Equal("#112233", settings.GetProperty("primaryColor").GetString());
        Assert.True(settings.TryGetProperty("version", out _));
        Assert.False(settings.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task Get_WhenTheRowIsMissing_Returns404WithACode()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Get, Base, cookie: cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("store_settings.not_found", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Put_WhenTheRowIsMissing_Returns404_AndCreatesNoRow()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        var cookie = await host.LoginAsync();

        var response = await host.SendAsync(HttpMethod.Put, Base, Body(version: 1), cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("store_settings.not_found", (await JsonOf(response)).GetProperty("code").GetString());
        await using var context = host.Database.CreateContext();
        Assert.Equal(0, await context.StoreSettings.CountAsync());
    }

    // --- Update ---

    [Fact]
    public async Task Put_SavesTheChange_ReturnsTheNewVersion_AndTheShopSeesIt()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedSettingsAsync(host);
        var cookie = await host.LoginAsync();
        var version = await VersionOfAsync(host, cookie);
        DateTimeOffset before;
        await using (var context = host.Database.CreateContext())
        {
            before = (await context.StoreSettings.AsNoTracking().SingleAsync()).UpdatedAt;
        }

        var response = await host.SendAsync(HttpMethod.Put, Base, Body(version), cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await JsonOf(response);
        Assert.Equal("New shop", saved.GetProperty("storeName").GetString());
        Assert.Equal("#2f5d8a", saved.GetProperty("primaryColor").GetString());
        Assert.NotEqual(version, saved.GetProperty("version").GetUInt32());

        // The public address shows the new values, and still has no version.
        var publicSettings = await JsonOf(await host.SendAsync(HttpMethod.Get, "/api/v1/store-settings"));
        Assert.Equal("New shop", publicSettings.GetProperty("storeName").GetString());
        Assert.Equal("EUR", publicSettings.GetProperty("defaultCurrency").GetString());
        Assert.Equal("#2f5d8a", publicSettings.GetProperty("primaryColor").GetString());
        Assert.Equal("info@example.com", publicSettings.GetProperty("contactEmail").GetString());
        Assert.Equal("+359 88 123 456", publicSettings.GetProperty("contactPhone").GetString());
        Assert.True(publicSettings.GetProperty("isDemo").GetBoolean());
        Assert.False(publicSettings.TryGetProperty("version", out _));

        // Still one row, and updated_at moved.
        await using var read = host.Database.CreateContext();
        Assert.Equal(1, await read.StoreSettings.CountAsync());
        Assert.True((await read.StoreSettings.AsNoTracking().SingleAsync()).UpdatedAt > before);
    }

    [Theory]
    [InlineData("", "EUR", "info@example.com", "#2F5D8A", "storeName")]
    [InlineData("Shop", "USD", "info@example.com", "#2F5D8A", "defaultCurrency")]
    [InlineData("Shop", "EUR", "info@example", "#2F5D8A", "contactEmail")]
    [InlineData("Shop", "EUR", "info@example.com", "#ffffff", "primaryColor")]
    [InlineData("Shop", "EUR", "info@example.com", "#12", "primaryColor")]
    public async Task Put_WithInvalidInput_Returns400WithTheFieldInTheErrors(
        string name, string currency, string email, string color, string field)
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedSettingsAsync(host);
        var cookie = await host.LoginAsync();
        var version = await VersionOfAsync(host, cookie);

        var response = await host.SendAsync(
            HttpMethod.Put, Base, Body(version, name: name, currency: currency, email: email, color: color), cookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var body = await JsonOf(response);
        Assert.Equal("validation.failed", body.GetProperty("code").GetString());
        Assert.Contains(
            body.GetProperty("errors").EnumerateObject(),
            e => string.Equals(e.Name, field, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Printko 3D", (await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie)))
            .GetProperty("storeName").GetString());
    }

    // --- Two administrators ---

    [Fact]
    public async Task Put_WithAStaleVersion_Returns409WithACode_AndKeepsTheOtherEdit()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedSettingsAsync(host);
        var cookie = await host.LoginAsync();
        var version = await VersionOfAsync(host, cookie);

        var first = await host.SendAsync(HttpMethod.Put, Base, Body(version, name: "First"), cookie);
        var second = await host.SendAsync(HttpMethod.Put, Base, Body(version, name: "Second"), cookie);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.True(second.IsProblemJson());
        Assert.Equal("store_settings.version_conflict", (await JsonOf(second)).GetProperty("code").GetString());
        var current = await JsonOf(await host.SendAsync(HttpMethod.Get, Base, cookie: cookie));
        Assert.Equal("First", current.GetProperty("storeName").GetString());
    }

    [Fact]
    public async Task TwoSavesWithTheSameVersionAtOnce_GiveOneSuccessAndOneConflict_AndNeverA500()
    {
        await using var host = await AuthApiHost.CreateAsync(postgres);
        await SeedSettingsAsync(host);
        var cookie = await host.LoginAsync();
        var version = await VersionOfAsync(host, cookie);

        var responses = await Task.WhenAll(
            host.SendAsync(HttpMethod.Put, Base, Body(version, name: "A"), cookie),
            host.SendAsync(HttpMethod.Put, Base, Body(version, name: "B"), cookie));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var context = host.Database.CreateContext();
        Assert.Equal(1, await context.StoreSettings.CountAsync());
    }
}
