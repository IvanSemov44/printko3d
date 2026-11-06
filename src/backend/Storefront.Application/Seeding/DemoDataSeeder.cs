using Storefront.Application.Catalog;
using Storefront.Application.Contracts;
using Storefront.Domain.Catalog;

namespace Storefront.Application.Seeding;

/// <summary>
/// Loads the demo catalog through the application services, so the same rules apply as for any other record.
/// Existing records are recognised by their natural keys (category slug, product slug) and left untouched.
/// </summary>
public sealed class DemoDataSeeder(
    IServiceManager services,
    IRepositoryManager repositories,
    ISchemaProbe schemaProbe)
{
    private const int ImagesPerProduct = 3;

    public async Task<SeedResult> SeedAsync(string? environmentName, CancellationToken cancellationToken)
    {
        SeedEnvironments.EnsureAllowed(environmentName);
        if (!await schemaProbe.IsUpToDateAsync(cancellationToken))
        {
            throw new SeedRefusedException(
                "The database schema is missing or not up to date, and seeding does not apply migrations. "
                + "Apply them first: dotnet ef database update --project src/backend/Storefront.Infrastructure");
        }

        var plan = DemoCatalog.Build();

        var categoryIds = new Dictionary<string, Guid>();
        var categoriesCreated = 0;
        foreach (var category in plan.Categories)
        {
            var existing = await repositories.Category.GetBySlugAsync(category.Slug, false, cancellationToken);
            if (existing is not null)
            {
                categoryIds[category.Slug] = existing.Id;
                continue;
            }

            categoryIds[category.Slug] = await services.Category.CreateAsync(
                new NewCategory(category.Name, category.Slug, null, category.Description, null, category.SortOrder),
                cancellationToken);
            categoriesCreated++;
        }

        var productsCreated = 0;
        var productsSkipped = 0;
        foreach (var product in plan.Products)
        {
            if (await repositories.Product.ExistsBySlugAsync(product.Slug, cancellationToken))
            {
                productsSkipped++;
                continue;
            }

            await CreateProductAsync(product, categoryIds[product.CategorySlug], cancellationToken);
            productsCreated++;
        }

        var settingsCreated = await services.StoreSettings.EnsureAsync(DemoCatalog.Settings, cancellationToken);
        return new SeedResult(categoriesCreated, productsCreated, productsSkipped, settingsCreated);
    }

    private async Task CreateProductAsync(PlannedProduct plan, Guid categoryId, CancellationToken cancellationToken)
    {
        var productId = await services.Product.CreateAsync(
            new NewProduct(
                categoryId,
                plan.Name,
                plan.Slug,
                plan.ShortDescription,
                plan.Description,
                plan.FulfilmentType,
                plan.LeadTimeDays),
            cancellationToken);

        var variantIds = new List<Guid>();
        foreach (var variant in plan.Variants)
        {
            variantIds.Add(await services.Product.AddVariantAsync(
                productId,
                new NewVariant(
                    variant.Sku,
                    variant.Name,
                    variant.Price,
                    variant.WeightGrams,
                    variant.CompareAtPrice,
                    variant.StockQuantity,
                    variant.Attributes,
                    SortOrder: variant.SortOrder),
                cancellationToken));
        }

        // The first image is the primary one. With several variants, the other images belong to the first two.
        for (var n = 1; n <= ImagesPerProduct; n++)
        {
            Guid? variantId = n > 1 && variantIds.Count > 1 ? variantIds[n - 2] : null;
            await services.Product.AddImageAsync(
                productId,
                new NewImage(
                    $"demo/{plan.CategorySlug}/{plan.Slug}-{n}.jpg",
                    $"Демо: {plan.Name}, снимка {n}",
                    variantId,
                    n - 1,
                    IsPrimary: n == 1),
                cancellationToken);
        }

        if (plan.Status != ProductStatus.Draft)
        {
            await services.Product.PublishAsync(productId, cancellationToken);
        }

        if (plan.Status == ProductStatus.Archived)
        {
            await services.Product.ArchiveAsync(productId, cancellationToken);
        }
    }
}
