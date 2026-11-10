using System.Net;
using Testcontainers.PostgreSql;

namespace Storefront.IntegrationTests;

public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17.6-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsReachable()
    {
        await using var factory = new ApiFactory(_postgres.GetConnectionString());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsServiceUnavailable_WhenDatabaseIsNotReachable()
    {
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("health.database_unreachable", await response.Content.ReadAsStringAsync());
    }
}
