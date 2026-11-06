namespace Storefront.Application.Seeding;

public sealed record SeedResult(int CategoriesCreated, int ProductsCreated, int ProductsSkipped, bool SettingsCreated);
