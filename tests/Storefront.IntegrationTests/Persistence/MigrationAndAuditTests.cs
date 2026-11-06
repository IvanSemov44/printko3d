using Microsoft.EntityFrameworkCore;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class MigrationAndAuditTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_ApplyToCleanDatabase_AndMatchTheModel()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        await using var context = database.CreateContext();

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(0, await context.Categories.CountAsync());
    }

    [Fact]
    public async Task Save_SetsCreatedAndUpdatedAt_OnInsert()
    {
        var database = await postgres.CreateDatabaseAsync();
        var created = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        await using var context = database.CreateContext(new FakeTimeProvider(created));
        var category = CatalogData.Category();

        context.Add(category);
        await context.SaveChangesAsync();

        Assert.Equal(created, category.CreatedAt);
        Assert.Equal(created, category.UpdatedAt);
    }

    [Fact]
    public async Task Save_UpdatesOnlyUpdatedAt_OnChange()
    {
        var database = await postgres.CreateDatabaseAsync();
        var created = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(created);
        await using var context = database.CreateContext(clock);
        var category = CatalogData.Category();
        context.Add(category);
        await context.SaveChangesAsync();

        clock.Advance(TimeSpan.FromHours(2));
        context.Entry(category).Property(c => c.SortOrder).CurrentValue = 5;
        await context.SaveChangesAsync();

        await using var read = database.CreateContext();
        var stored = await read.Categories.AsNoTracking().SingleAsync();
        Assert.Equal(created, stored.CreatedAt);
        Assert.Equal(created.AddHours(2), stored.UpdatedAt);
    }
}
