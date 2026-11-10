using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

/// <summary>The rules for the fields of a product and for changing its fulfilment type. No database access.</summary>
internal static class ProductRules
{
    private const int NameMaxLength = 200;
    private const int SeoTitleMaxLength = 200;
    private const int SeoDescriptionMaxLength = 500;

    /// <summary>Checks the fields of a product and returns the lead time to store: only made-to-order products have one.</summary>
    public static int? Validate(
        string? name,
        string? slug,
        string? shortDescription,
        FulfilmentType fulfilmentType,
        int? leadTimeDays,
        string? seoTitle,
        string? seoDescription)
    {
        var errors = new FieldErrors();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("name", "Name is required.");
        }
        else if (name.Trim().Length > NameMaxLength)
        {
            errors.Add("name", $"Name must be at most {NameMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            errors.Add("slug", "Slug is required.");
        }
        else if (!SlugRules.IsValid(slug))
        {
            errors.Add("slug", SlugRules.FormatMessage);
        }

        if (shortDescription is { Length: > Product.ShortDescriptionMaxLength })
        {
            errors.Add("shortDescription", $"The short description is at most {Product.ShortDescriptionMaxLength} characters.");
        }

        if (seoTitle is { Length: > SeoTitleMaxLength })
        {
            errors.Add("seoTitle", $"The SEO title is at most {SeoTitleMaxLength} characters.");
        }

        if (seoDescription is { Length: > SeoDescriptionMaxLength })
        {
            errors.Add("seoDescription", $"The SEO description is at most {SeoDescriptionMaxLength} characters.");
        }

        if (fulfilmentType == FulfilmentType.MadeToOrder && leadTimeDays is null or <= 0)
        {
            errors.Add("leadTimeDays", "Lead time must be at least 1 day for made-to-order products.");
        }

        errors.ThrowIfAny("The product is not valid.");
        return fulfilmentType == FulfilmentType.MadeToOrder ? leadTimeDays : null;
    }

    /// <summary>
    /// The stock to write to the variants when the product changes its fulfilment type (empty when it does not).
    /// Made-to-order products keep no stock. For in-stock the caller must give the stock of every variant.
    /// </summary>
    public static Dictionary<Guid, int?> StockAfterTypeChange(Product product, UpdateProduct input)
    {
        var stock = new Dictionary<Guid, int?>();
        if (input.FulfilmentType == product.FulfilmentType)
        {
            return stock;
        }

        if (input.FulfilmentType == FulfilmentType.MadeToOrder)
        {
            foreach (var variant in product.Variants)
            {
                stock[variant.Id] = null;
            }

            return stock;
        }

        var errors = new FieldErrors();
        var given = input.VariantStock ?? new Dictionary<Guid, int>();
        foreach (var variant in product.Variants)
        {
            if (!given.TryGetValue(variant.Id, out var quantity))
            {
                errors.Add("variantStock", $"The stock of variant {variant.Sku} is required.");
            }
            else if (quantity < 0)
            {
                errors.Add("variantStock", $"The stock of variant {variant.Sku} must not be negative.");
            }
            else
            {
                stock[variant.Id] = quantity;
            }
        }

        foreach (var id in given.Keys.Where(id => product.Variants.All(v => v.Id != id)))
        {
            errors.Add("variantStock", $"Variant {id} does not belong to this product.");
        }

        errors.ThrowIfAny("The product is not valid.");
        return stock;
    }
}
