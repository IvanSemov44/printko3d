using Storefront.Application.Catalog;
using Storefront.Application.Seeding;
using Storefront.Domain.Catalog;

namespace Storefront.UnitTests;

public sealed class DemoDataSeederTests
{
    private readonly FakeRepositoryManager _repositories = new();
    private readonly StubSchemaProbe _schema = new(true);

    private DemoDataSeeder CreateSeeder() =>
        new(new ServiceManager(_repositories, new FakeImageStorage()), _repositories, _schema);

    [Fact]
    public void Plan_HasSixteenProductsInFourCategories()
    {
        var plan = DemoCatalog.Build();

        Assert.Equal(4, plan.Categories.Count);
        Assert.Equal(16, plan.Products.Count);
        var counts = plan.Products.GroupBy(p => p.CategorySlug).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(5, counts["3d-puzzles"]);
        Assert.Equal(5, counts["figurines"]);
        Assert.Equal(3, counts["decor"]);
        Assert.Equal(3, counts["gifts"]);
    }

    [Fact]
    public void Plan_CoversTheStatesTheStorefrontMustHandle()
    {
        var plan = DemoCatalog.Build();

        Assert.Contains(plan.Products, p => p.Status == ProductStatus.Draft);
        Assert.Contains(plan.Products, p => p.Status == ProductStatus.Archived);
        Assert.Contains(plan.Products, p => p.FulfilmentType == FulfilmentType.InStock
            && p.Variants.All(v => v.StockQuantity == 0));
        Assert.All(
            plan.Products.Where(p => p.FulfilmentType == FulfilmentType.MadeToOrder),
            p => Assert.NotNull(p.LeadTimeDays));
    }

    [Fact]
    public void Plan_KeysAreUnique_AndNumbersStayInRange()
    {
        var plan = DemoCatalog.Build();

        Assert.Equal(plan.Products.Count, plan.Products.Select(p => p.Slug).Distinct().Count());
        var variants = plan.Products.SelectMany(p => p.Variants).ToList();
        Assert.Equal(variants.Count, variants.Select(v => v.Sku).Distinct().Count());
        Assert.All(variants, v =>
        {
            Assert.InRange(v.Price, 800, 18000);
            Assert.Equal(0, v.Price % 50);
            Assert.InRange(v.WeightGrams, 30, 600);
        });
        Assert.All(plan.Products, p => Assert.True(p.ShortDescription.Length <= Product.ShortDescriptionMaxLength));
    }

    [Fact]
    public void Plan_IsTheSameEveryTime()
    {
        static string Fingerprint(DemoCatalogPlan plan) => string.Join(
            "|",
            plan.Products.SelectMany(p => p.Variants.Select(v =>
                $"{p.Slug}/{v.Sku}/{v.Price}/{v.CompareAtPrice}/{v.WeightGrams}/{v.StockQuantity}")));

        Assert.Equal(Fingerprint(DemoCatalog.Build()), Fingerprint(DemoCatalog.Build()));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    public async Task Seeding_IsRefusedOutsideAllowedEnvironments_AndNothingIsCreated(string environment)
    {
        await Assert.ThrowsAsync<SeedRefusedException>(
            () => CreateSeeder().SeedAsync(environment, default));

        Assert.Empty(_repositories.Products);
        Assert.Empty(_repositories.Categories);
        Assert.Empty(_repositories.Settings);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Demo")]
    public async Task Seeding_IsAllowedInDevelopmentAndDemo(string environment)
    {
        var result = await CreateSeeder().SeedAsync(environment, default);

        Assert.Equal(16, result.ProductsCreated);
    }

    [Fact]
    public async Task Seeding_WithoutASchema_IsRefused_AndNothingIsCreated()
    {
        var seeder = new DemoDataSeeder(new ServiceManager(_repositories, new FakeImageStorage()), _repositories, new StubSchemaProbe(false));

        var exception = await Assert.ThrowsAsync<SeedRefusedException>(() => seeder.SeedAsync("Development", default));

        Assert.Contains("migrations", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_repositories.Products);
    }

    [Fact]
    public async Task Seeding_CreatesEveryPublishedProductWithVariantsAndOnePrimaryImage()
    {
        await CreateSeeder().SeedAsync("Development", default);

        Assert.Equal(16, _repositories.Products.Count);
        Assert.Equal(4, _repositories.Categories.Count);
        var published = _repositories.Products.Where(p => p.Status == ProductStatus.Published).ToList();
        Assert.Equal(14, published.Count);
        Assert.All(published, p =>
        {
            Assert.NotEmpty(p.Variants);
            Assert.NotEmpty(p.Images);
            Assert.Single(p.Images, i => i.IsPrimary);
        });
        Assert.Contains(_repositories.Products.SelectMany(p => p.Images), i => i.VariantId is not null);
        Assert.All(
            _repositories.Products.SelectMany(p => p.Images),
            i => Assert.DoesNotContain("://", i.StorageKey, StringComparison.Ordinal));
        Assert.Single(_repositories.Products, p => p.Status == ProductStatus.Draft);
        Assert.Single(_repositories.Products, p => p.Status == ProductStatus.Archived);
    }

    [Fact]
    public async Task Seeding_CreatesOneDemoSettingsRow()
    {
        await CreateSeeder().SeedAsync("Development", default);

        var settings = Assert.Single(_repositories.Settings);
        Assert.True(settings.IsDemo);
        Assert.Equal("Принтко 3Д", settings.StoreName);
    }

    [Fact]
    public async Task Seeding_CreatesTheDemoShippingTariff_AndKeepsAChangedOneOnASecondRun()
    {
        await CreateSeeder().SeedAsync("Development", default);

        Assert.Equal(DemoCatalog.ShippingSteps.Count, _repositories.Rates.Count);
        Assert.All(_repositories.Rates, r => Assert.Equal("address", r.MethodCode));

        _repositories.Rates[0].Price = 12345;
        await CreateSeeder().SeedAsync("Development", default);

        Assert.Equal(DemoCatalog.ShippingSteps.Count, _repositories.Rates.Count);
        Assert.Equal(12345, _repositories.Rates[0].Price);
    }

    [Fact]
    public async Task Seeding_Twice_DoesNotDuplicateAnything()
    {
        await CreateSeeder().SeedAsync("Development", default);

        var second = await CreateSeeder().SeedAsync("Development", default);

        Assert.Equal(new SeedResult(0, 0, 16, false), second);
        Assert.Equal(16, _repositories.Products.Count);
        Assert.Equal(4, _repositories.Categories.Count);
        Assert.Single(_repositories.Settings);
    }

    private sealed class StubSchemaProbe(bool result) : ISchemaProbe
    {
        public Task<bool> IsUpToDateAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
