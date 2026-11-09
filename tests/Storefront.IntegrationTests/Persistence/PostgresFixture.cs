using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Storefront.IntegrationTests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17.6-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public async Task<TestDatabase> CreateDatabaseAsync(bool migrate = true)
    {
        // Tests of one collection run one after another. Each finished test leaves idle pooled connections to its own
        // database, and the container allows only 100 connections, so release them before opening a new database.
        NpgsqlConnection.ClearAllPools();
        var name = $"test_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = name,
        }.ConnectionString;
        var database = new TestDatabase(connectionString);
        if (migrate)
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
        }

        return database;
    }
}

public sealed class TestDatabase(string connectionString)
{
    public string ConnectionString => connectionString;

    public StorefrontDbContext CreateContext(TimeProvider? timeProvider = null)
    {
        var options = new DbContextOptionsBuilder<StorefrontDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new StorefrontDbContext(options, timeProvider ?? TimeProvider.System);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
