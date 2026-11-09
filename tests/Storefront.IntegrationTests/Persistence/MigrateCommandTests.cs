using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Api.Commands;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class MigrateCommandTests(PostgresFixture postgres)
{
    private sealed record Outcome(int ExitCode, string Output, string Error);

    private static async Task<Outcome> RunMigrateAsync(string connectionString)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await MigrateCommand.RunAsync(
            [MigrateCommand.Name, $"--Database:ConnectionString={connectionString}"], output, error, default);
        return new Outcome(exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task Migrate_OnACleanDatabase_AppliesEveryMigration()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);

        var outcome = await RunMigrateAsync(database.ConnectionString);

        Assert.Equal(0, outcome.ExitCode);
        await using var read = database.CreateContext();
        Assert.NotEmpty(await read.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await read.Database.GetPendingMigrationsAsync());
        Assert.Contains("applied", outcome.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrate_CanTakeTheDatabaseAsAPostgresAddress()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        var builder = new NpgsqlConnectionStringBuilder(database.ConnectionString);
        var url = $"postgres://{Uri.EscapeDataString(builder.Username!)}:{Uri.EscapeDataString(builder.Password!)}"
            + $"@{builder.Host}:{builder.Port}/{builder.Database}";
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MigrateCommand.RunAsync(
            [MigrateCommand.Name, $"--Database:Url={url}"], output, error, default);

        Assert.Equal(0, exitCode);
        await using var read = database.CreateContext();
        Assert.Empty(await read.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Migrate_RunTwice_ChangesNothingAndStillSucceeds()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        await RunMigrateAsync(database.ConnectionString);
        await using var before = database.CreateContext();
        var appliedBefore = (await before.Database.GetAppliedMigrationsAsync()).ToList();

        var second = await RunMigrateAsync(database.ConnectionString);

        Assert.Equal(0, second.ExitCode);
        Assert.Contains("already up to date", second.Output, StringComparison.Ordinal);
        await using var after = database.CreateContext();
        Assert.Equal(appliedBefore, (await after.Database.GetAppliedMigrationsAsync()).ToList());
    }

    [Fact]
    public async Task Migrate_WhenTheDatabaseIsNotReachable_FailsWithoutLeakingTheConnectionDetails()
    {
        const string unreachable = "Host=127.0.0.1;Port=1;Database=x;Username=secret-user;Password=secret-pass;Timeout=2";

        var outcome = await RunMigrateAsync(unreachable);

        Assert.NotEqual(0, outcome.ExitCode);
        Assert.DoesNotContain("secret-pass", outcome.Output + outcome.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-user", outcome.Output + outcome.Error, StringComparison.Ordinal);
    }
}
