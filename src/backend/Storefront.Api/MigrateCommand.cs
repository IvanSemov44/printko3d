using Npgsql;
using Storefront.Application.Migrations;
using Storefront.Infrastructure;

namespace Storefront.Api;

/// <summary>
/// `Storefront.Api migrate`: applies the pending database migrations and exits. It starts no web server,
/// and the API never does this on its own at startup (several copies must not migrate at the same time).
/// Safe to repeat: with nothing pending it changes nothing. Any further arguments are normal configuration
/// arguments (for example `--environment=Demo`).
/// </summary>
public static class MigrateCommand
{
    public const string Name = "migrate";

    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Name, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.Services.AddInfrastructure(builder.Configuration);

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        try
        {
            var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
            var applied = await migrator.MigrateAsync(cancellationToken);
            await output.WriteLineAsync(applied.Count == 0
                ? "Migrate finished: the database was already up to date."
                : $"Migrate finished: {applied.Count} migrations applied ({string.Join(", ", applied)}).");
            return 0;
        }
        catch (NpgsqlException)
        {
            // The message of a database exception can carry the host and user name; keep it out of the output.
            await error.WriteLineAsync("Migrate failed: the database is not reachable or refused the change.");
            return 1;
        }
    }
}
