using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Storefront.Application.Health;

namespace Storefront.Infrastructure.Health;

public sealed class NpgsqlDatabaseProbe(
    IOptions<DatabaseOptions> options,
    ILogger<NpgsqlDatabaseProbe> logger) : IDatabaseProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await using var connection = new NpgsqlConnection(options.Value.ConnectionString);
            await connection.OpenAsync(timeout.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(timeout.Token);
            return true;
        }
        catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException or TimeoutException
            && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Database health check failed");
            return false;
        }
    }
}
