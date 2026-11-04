using Storefront.Application.Health;

namespace Storefront.UnitTests;

public class HealthServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_ReflectsDatabaseReachability(bool databaseReachable)
    {
        var service = new HealthService(new StubDatabaseProbe(databaseReachable));

        var status = await service.CheckAsync(CancellationToken.None);

        Assert.Equal(databaseReachable, status.IsHealthy);
        Assert.Equal(databaseReachable, status.DatabaseReachable);
    }

    private sealed class StubDatabaseProbe(bool result) : IDatabaseProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
