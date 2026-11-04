namespace Storefront.Application.Health;

public sealed class HealthService(IDatabaseProbe databaseProbe)
{
    public async Task<HealthStatus> CheckAsync(CancellationToken cancellationToken)
    {
        var databaseReachable = await databaseProbe.CanConnectAsync(cancellationToken);
        return new HealthStatus(databaseReachable, databaseReachable);
    }
}
