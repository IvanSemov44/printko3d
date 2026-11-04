namespace Storefront.Application.Health;

public sealed record HealthStatus(bool IsHealthy, bool DatabaseReachable);
