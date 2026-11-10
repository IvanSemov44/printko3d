using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

public sealed record NewProduct(
    Guid CategoryId,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    FulfilmentType FulfilmentType,
    int? LeadTimeDays = null,
    string? SeoTitle = null,
    string? SeoDescription = null);
