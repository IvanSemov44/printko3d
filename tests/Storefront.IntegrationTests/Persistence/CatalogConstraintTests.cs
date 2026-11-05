using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Domain.Catalog;

namespace Storefront.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class CatalogConstraintTests(PostgresFixture postgres)
{
    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        var postgresException = exception as PostgresException ?? exception?.InnerException as PostgresException;
        return postgresException?.SqlState;
    }

    private static async Task<(Guid CategoryId, Guid ProductId)> SeedProductAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var category = CatalogData.Category();
        var product = CatalogData.Product(category.Id);
        CatalogData.Variant(product);
        context.Add(category);
        context.Add(product);
        await context.SaveChangesAsync();
        return (category.Id, product.Id);
    }

    [Fact]
    public async Task Category_RejectsDuplicateSlug_AndAcceptsDistinctSlug()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using (var context = database.CreateContext())
        {
            context.Add(CatalogData.Category("figures"));
            await context.SaveChangesAsync();
        }

        await using var duplicate = database.CreateContext();
        duplicate.Add(CatalogData.Category("figures"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var distinct = database.CreateContext();
        distinct.Add(CatalogData.Category("puzzles"));
        Assert.Equal(1, await distinct.SaveChangesAsync());
    }

    [Fact]
    public async Task Product_RejectsDuplicateSlug_AndAcceptsDistinctSlug()
    {
        var database = await postgres.CreateDatabaseAsync();
        var (categoryId, _) = await SeedProductAsync(database);

        await using var duplicate = database.CreateContext();
        var sameSlug = CatalogData.Product(categoryId, "dragon");
        CatalogData.Variant(sameSlug, "OTHER-1");
        duplicate.Add(sameSlug);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var distinct = database.CreateContext();
        var otherSlug = CatalogData.Product(categoryId, "phoenix");
        CatalogData.Variant(otherSlug, "OTHER-2");
        distinct.Add(otherSlug);
        Assert.Equal(2, await distinct.SaveChangesAsync());
    }

    [Fact]
    public async Task Variant_RejectsDuplicateSku_AndAcceptsDistinctSku()
    {
        var database = await postgres.CreateDatabaseAsync();
        var (categoryId, _) = await SeedProductAsync(database);

        await using var duplicate = database.CreateContext();
        var product = CatalogData.Product(categoryId, "phoenix");
        CatalogData.Variant(product, "DRG-001");
        duplicate.Add(product);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => duplicate.SaveChangesAsync()));

        await using var distinct = database.CreateContext();
        var other = CatalogData.Product(categoryId, "phoenix");
        CatalogData.Variant(other, "PHX-001");
        distinct.Add(other);
        Assert.Equal(2, await distinct.SaveChangesAsync());
    }

    [Fact]
    public async Task Product_RejectsUnknownCategory()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Add(CatalogData.Product(Guid.CreateVersion7()));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Category_RejectsUnknownParent()
    {
        var database = await postgres.CreateDatabaseAsync();
        var orphanParent = CatalogData.Category("parent");
        await using var context = database.CreateContext();
        context.Add(CatalogData.Category("child", orphanParent));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task DeletingCategoryWithProducts_IsRejected()
    {
        var database = await postgres.CreateDatabaseAsync();
        var (categoryId, _) = await SeedProductAsync(database);

        await using var context = database.CreateContext();
        var state = await SqlStateOfAsync(
            () => context.Categories.Where(c => c.Id == categoryId).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, state);
    }

    [Fact]
    public async Task DeletingProductWithVariants_IsRejected_WithoutCascade()
    {
        var database = await postgres.CreateDatabaseAsync();
        var (_, productId) = await SeedProductAsync(database);

        await using var context = database.CreateContext();
        var state = await SqlStateOfAsync(
            () => context.Products.Where(p => p.Id == productId).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, state);
        Assert.Equal(1, await context.ProductVariants.CountAsync());
    }

    [Fact]
    public async Task Image_RejectsUnknownVariant()
    {
        var database = await postgres.CreateDatabaseAsync();
        var (categoryId, _) = await SeedProductAsync(database);

        await using var context = database.CreateContext();
        var product = CatalogData.Product(categoryId, "phoenix");
        CatalogData.Variant(product, "PHX-001");
        CatalogData.Image(product, "a.jpg", variantId: Guid.CreateVersion7());
        context.Add(product);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlStateOfAsync(() => context.SaveChangesAsync()));
    }

    [Fact]
    public async Task Image_AllowsOnlyOnePrimaryPerProduct()
    {
        var database = await postgres.CreateDatabaseAsync();
        var (categoryId, productId) = await SeedProductAsync(database);

        await using (var context = database.CreateContext())
        {
            var product = await context.Products.Include(p => p.Images).SingleAsync(p => p.Id == productId);
            CatalogData.Image(product, "a.jpg", isPrimary: true);
            CatalogData.Image(product, "b.jpg");
            CatalogData.Image(product, "c.jpg");
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var other = CatalogData.Product(categoryId, "phoenix");
            CatalogData.Variant(other, "PHX-001");
            CatalogData.Image(other, "d.jpg", isPrimary: true);
            context.Add(other);
            await context.SaveChangesAsync();
        }

        await using var second = database.CreateContext();
        var target = await second.Products.Include(p => p.Images).SingleAsync(p => p.Id == productId);
        CatalogData.Image(target, "e.jpg", isPrimary: true);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(() => second.SaveChangesAsync()));
    }

    [Theory]
    [InlineData(
        "UPDATE product_variants SET price = -1",
        "UPDATE product_variants SET price = 0")]
    [InlineData(
        "UPDATE product_variants SET compare_at_price = -1",
        "UPDATE product_variants SET compare_at_price = 0")]
    [InlineData(
        "UPDATE product_variants SET stock_quantity = -1",
        "UPDATE product_variants SET stock_quantity = 0")]
    public async Task Variant_RejectsNegativeValues_AndAcceptsZero(string negativeSql, string zeroSql)
    {
        var database = await postgres.CreateDatabaseAsync();
        await SeedProductAsync(database);
        await using var context = database.CreateContext();

        var negative = await SqlStateOfAsync(() => context.Database.ExecuteSqlRawAsync(negativeSql));
        Assert.Equal(PostgresErrorCodes.CheckViolation, negative);

        var affected = await context.Database.ExecuteSqlRawAsync(zeroSql);
        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task Product_RequiresLeadTime_WhenMadeToOrder()
    {
        var database = await postgres.CreateDatabaseAsync();
        var (categoryId, _) = await SeedProductAsync(database);
        await using (var context = database.CreateContext())
        {
            var product = CatalogData.Product(categoryId, "puzzle", FulfilmentType.MadeToOrder, leadTimeDays: 7);
            CatalogData.Variant(product, "PZL-001");
            context.Add(product);
            await context.SaveChangesAsync();
        }

        await using var check = database.CreateContext();
        var withoutLeadTime = await SqlStateOfAsync(() => check.Database.ExecuteSqlRawAsync(
            "UPDATE products SET lead_time_days = NULL WHERE slug = 'puzzle'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, withoutLeadTime);

        var inStockWithoutLeadTime = await check.Database.ExecuteSqlRawAsync(
            "UPDATE products SET lead_time_days = NULL WHERE slug = 'dragon'");
        Assert.Equal(1, inStockWithoutLeadTime);
    }

    [Theory]
    [InlineData("UPDATE products SET status = 'Deleted'")]
    [InlineData("UPDATE products SET fulfilment_type = 'Dropship'")]
    public async Task Product_RejectsUnknownEnumValues(string sql)
    {
        var database = await postgres.CreateDatabaseAsync();
        await SeedProductAsync(database);
        await using var context = database.CreateContext();

        var state = await SqlStateOfAsync(() => context.Database.ExecuteSqlRawAsync(sql));

        Assert.Equal(PostgresErrorCodes.CheckViolation, state);
    }

    [Fact]
    public async Task StoreSettings_AllowsOneRow_AndRejectsASecond()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using (var context = database.CreateContext())
        {
            context.Add(CatalogData.Settings());
            await context.SaveChangesAsync();
        }

        await using var second = database.CreateContext();
        var state = await SqlStateOfAsync(() => second.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO store_settings
                (id, store_name, default_currency, default_locale, contact_email, contact_phone,
                 primary_color, is_demo, created_at, updated_at)
            SELECT gen_random_uuid(), store_name, default_currency, default_locale, contact_email, contact_phone,
                 primary_color, is_demo, created_at, updated_at
            FROM store_settings
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, state);
        Assert.Equal(1, await second.StoreSettings.CountAsync());
    }

    [Fact]
    public async Task Attributes_AreStoredAsJsonb_AndRoundTrip()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using (var context = database.CreateContext())
        {
            var category = CatalogData.Category();
            var product = CatalogData.Product(category.Id);
            CatalogData.Variant(product, attributes: new Dictionary<string, string> { ["color"] = "blue" });
            context.Add(category);
            context.Add(product);
            await context.SaveChangesAsync();
        }

        await using var read = database.CreateContext();
        var variant = await read.ProductVariants.AsNoTracking().SingleAsync();
        Assert.Equal("blue", variant.Attributes["color"]);

        var type = await read.Database
            .SqlQueryRaw<string>(
                "SELECT data_type AS \"Value\" FROM information_schema.columns " +
                "WHERE table_name = 'product_variants' AND column_name = 'attributes'")
            .SingleAsync();
        Assert.Equal("jsonb", type);
    }

    [Fact]
    public async Task Products_HaveIndexOnCategoryAndStatus()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var context = database.CreateContext();

        var definitions = await context.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'products'")
            .ToListAsync();

        Assert.Contains(definitions, d => d.Contains("(category_id, status)", StringComparison.Ordinal));
    }
}
