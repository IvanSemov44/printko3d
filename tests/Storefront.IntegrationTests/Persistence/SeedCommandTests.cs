using Microsoft.EntityFrameworkCore;
using Storefront.Api;
using Storefront.Domain.Catalog;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class SeedCommandTests(PostgresFixture postgres)
{
    private sealed record Outcome(int ExitCode, string Output, string Error);

    private static async Task<Outcome> RunSeedAsync(TestDatabase database, string? environment = "Development")
    {
        var args = new List<string> { SeedCommand.Name, $"--Database:ConnectionString={database.ConnectionString}" };
        if (environment is not null)
        {
            args.Add($"--environment={environment}");
        }

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await SeedCommand.RunAsync([.. args], output, error, default);
        return new Outcome(exitCode, output.ToString(), error.ToString());
    }

    private static async Task<string> FingerprintAsync(TestDatabase database)
    {
        await using var read = database.CreateContext();
        var products = await read.Products.AsNoTracking().Include(p => p.Variants)
            .OrderBy(p => p.Slug).ToListAsync();
        return string.Join(
            "|",
            products.SelectMany(p => p.Variants.OrderBy(v => v.Sku).Select(v =>
                $"{p.Name}/{p.Slug}/{p.Status}/{p.LeadTimeDays}/{v.Sku}/{v.Price}/{v.CompareAtPrice}/{v.WeightGrams}/{v.StockQuantity}")));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Demo")]
    public async Task Seed_LoadsTheWholeDemoCatalog(string environment)
    {
        var database = await postgres.CreateDatabaseAsync();

        var outcome = await RunSeedAsync(database, environment);

        Assert.Equal(0, outcome.ExitCode);
        await using var read = database.CreateContext();
        Assert.Equal(4, await read.Categories.CountAsync());
        var products = await read.Products.AsNoTracking()
            .Include(p => p.Variants).Include(p => p.Images).ToListAsync();
        Assert.Equal(16, products.Count);
        var categories = await read.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Slug);
        var perCategory = products.GroupBy(p => categories[p.CategoryId]).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["3d-puzzles"] = 5, ["figurines"] = 5, ["decor"] = 3, ["gifts"] = 3 }, perCategory);

        var settings = await read.StoreSettings.AsNoTracking().SingleAsync();
        Assert.True(settings.IsDemo);
        Assert.Equal("Принтко 3Д", settings.StoreName);
    }

    [Fact]
    public async Task Seed_CreatesDraftArchivedAndOutOfStockProducts()
    {
        var database = await postgres.CreateDatabaseAsync();
        await RunSeedAsync(database);

        await using var read = database.CreateContext();
        var products = await read.Products.AsNoTracking().Include(p => p.Variants).ToListAsync();
        Assert.Contains(products, p => p.Status == ProductStatus.Draft);
        Assert.Contains(products, p => p.Status == ProductStatus.Archived);
        Assert.Contains(products, p => p.FulfilmentType == FulfilmentType.InStock
            && p.Variants.All(v => v.StockQuantity == 0));
        Assert.All(
            products.Where(p => p.FulfilmentType == FulfilmentType.MadeToOrder),
            p => Assert.NotNull(p.LeadTimeDays));
    }

    [Fact]
    public async Task Seed_GivesEveryPublishedProductVariantsAndExactlyOnePrimaryImage()
    {
        var database = await postgres.CreateDatabaseAsync();
        await RunSeedAsync(database);

        await using var read = database.CreateContext();
        var products = await read.Products.AsNoTracking()
            .Include(p => p.Variants).Include(p => p.Images).ToListAsync();
        var published = products.Where(p => p.Status == ProductStatus.Published).ToList();
        Assert.NotEmpty(published);
        Assert.All(published, p =>
        {
            Assert.NotEmpty(p.Variants);
            Assert.NotEmpty(p.Images);
            Assert.Single(p.Images, i => i.IsPrimary);
        });
        var images = products.SelectMany(p => p.Images).ToList();
        Assert.Contains(images, i => i.VariantId is not null);
        Assert.All(products, p => Assert.All(
            p.Images.Where(i => i.VariantId is not null),
            i => Assert.Contains(p.Variants, v => v.Id == i.VariantId)));
        Assert.All(images, i => Assert.DoesNotContain("://", i.StorageKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seed_OnTwoCleanDatabases_GivesTheSameNamesSlugsSkusPricesAndStock()
    {
        var first = await postgres.CreateDatabaseAsync();
        var second = await postgres.CreateDatabaseAsync();

        await RunSeedAsync(first);
        await RunSeedAsync(second);

        var fingerprint = await FingerprintAsync(first);
        Assert.NotEmpty(fingerprint);
        Assert.Equal(fingerprint, await FingerprintAsync(second));
    }

    [Fact]
    public async Task Seed_RunTwice_ChangesNothingAndStillSucceeds()
    {
        var database = await postgres.CreateDatabaseAsync();
        await RunSeedAsync(database);
        await using var before = database.CreateContext();
        var idsBefore = await before.Products.AsNoTracking().Select(p => p.Id).OrderBy(id => id).ToListAsync();
        var fingerprint = await FingerprintAsync(database);

        var second = await RunSeedAsync(database);

        Assert.Equal(0, second.ExitCode);
        await using var after = database.CreateContext();
        Assert.Equal(idsBefore, await after.Products.AsNoTracking().Select(p => p.Id).OrderBy(id => id).ToListAsync());
        Assert.Equal(4, await after.Categories.CountAsync());
        Assert.Equal(1, await after.StoreSettings.CountAsync());
        Assert.Equal(48, await after.ProductImages.CountAsync());
        Assert.Equal(fingerprint, await FingerprintAsync(database));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData(null)]
    public async Task Seed_OutsideDevelopmentAndDemo_IsRefusedAndWritesNothing(string? environment)
    {
        var database = await postgres.CreateDatabaseAsync();

        var outcome = await RunSeedAsync(database, environment);

        Assert.NotEqual(0, outcome.ExitCode);
        Assert.Contains("Development", outcome.Error, StringComparison.Ordinal);
        await using var read = database.CreateContext();
        Assert.Empty(await read.Products.ToListAsync());
        Assert.Empty(await read.Categories.ToListAsync());
        Assert.Empty(await read.StoreSettings.ToListAsync());
    }

    [Fact]
    public async Task Seed_WithoutTheSchema_IsRefusedAndDoesNotApplyMigrations()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);

        var outcome = await RunSeedAsync(database);

        Assert.NotEqual(0, outcome.ExitCode);
        Assert.Contains("migrations", outcome.Error, StringComparison.OrdinalIgnoreCase);
        await using var read = database.CreateContext();
        Assert.Empty(await read.Database.GetAppliedMigrationsAsync());
    }
}
