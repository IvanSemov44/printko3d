using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Application.Auth;
using Storefront.Infrastructure.Identity;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

/// <summary>An API over a fresh database with one administrator. Cookies are passed by hand, so each test controls the session.</summary>
internal sealed class AuthApiHost : IAsyncDisposable
{
    public const string AdminEmail = "admin@example.test";

    // A made-up password for tests only.
    public const string AdminPassword = "correct horse battery staple";

    public const string CookieName = "__Host-printko-admin";

    private readonly WebApplicationFactory<Program> _factory;

    private AuthApiHost(
        TestDatabase database,
        string environment,
        IReadOnlyDictionary<string, string> settings,
        Action<IServiceCollection>? configureServices)
    {
        Database = database;
        _factory = new ApiFactory(database.ConnectionString).WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            if (configureServices is not null)
            {
                builder.ConfigureServices(configureServices);
            }
        });
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // The session cookie is Secure, so the tests talk to the API over https.
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
    }

    public TestDatabase Database { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services => _factory.Services;

    public static async Task<AuthApiHost> CreateAsync(
        PostgresFixture postgres,
        string environment = "Development",
        int attemptsPerMinute = 1000,
        bool createAdmin = true,
        TestDatabase? database = null,
        Action<IServiceCollection>? configureServices = null,
        bool loginEnabled = true)
    {
        database ??= await postgres.CreateDatabaseAsync();
        var host = new AuthApiHost(
            database,
            environment,
            new Dictionary<string, string>
            {
                ["AdminLogin:LoginAttemptsPerMinute"] = attemptsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["AdminLogin:Enabled"] = loginEnabled ? "true" : "false",
            },
            configureServices);
        if (createAdmin)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<IAdminAccountService>();
            await accounts.EnsureAdminAsync(AdminEmail, AdminPassword, default);
        }

        return host;
    }

    public async Task CreateUserWithoutRoleAsync(string email, string password)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        var result = await users.CreateAsync(new AdminUser { UserName = email, Email = email }, password);
        Assert.True(result.Succeeded);
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, object? body = null, string? cookie = null, bool csrfHeader = true)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (csrfHeader)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> LoginRawAsync(string email, string password) =>
        SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password });

    /// <summary>Signs in and returns the cookie to send back ("name=value").</summary>
    public async Task<string> LoginAsync(string email = AdminEmail, string password = AdminPassword)
    {
        var response = await LoginRawAsync(email, password);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return CookieOf(response) ?? throw new InvalidOperationException("The login set no session cookie.");
    }

    public static string? SetCookieHeaderOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    public static string? CookieOf(HttpResponseMessage response) => SetCookieHeaderOf(response)?.Split(';')[0];

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }
}

internal static class ResponseExtensions
{
    public static bool IsProblemJson(this HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == "application/problem+json";
}
