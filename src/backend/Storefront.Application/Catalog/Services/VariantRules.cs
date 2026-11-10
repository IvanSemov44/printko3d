using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

/// <summary>The rules for the fields of a variant. No database access.</summary>
internal static class VariantRules
{
    private const int SkuMaxLength = 100;
    private const int VariantNameMaxLength = 200;
    private const int MaxAttributes = 10;
    private const int AttributeKeyMaxLength = 50;
    private const int AttributeValueMaxLength = 100;

    public static FieldErrors Validate(FulfilmentType fulfilmentType, VariantFields fields)
    {
        var (sku, name, price, weightGrams, compareAtPrice, stockQuantity, attributes) = fields;
        var errors = new FieldErrors();
        if (string.IsNullOrWhiteSpace(sku))
        {
            errors.Add("sku", "SKU is required.");
        }
        else if (sku.Trim().Length > SkuMaxLength)
        {
            errors.Add("sku", $"SKU must be at most {SkuMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("name", "Name is required.");
        }
        else if (name.Trim().Length > VariantNameMaxLength)
        {
            errors.Add("name", $"Name must be at most {VariantNameMaxLength} characters.");
        }

        if (price < 0)
        {
            errors.Add("price", "Price must not be negative.");
        }

        if (weightGrams < 0)
        {
            errors.Add("weightGrams", "Weight must not be negative.");
        }

        if (compareAtPrice < 0)
        {
            errors.Add("compareAtPrice", "The compare-at price must not be negative.");
        }
        else if (compareAtPrice < price)
        {
            errors.Add("compareAtPrice", "The compare-at price must not be less than the price.");
        }

        if (fulfilmentType == FulfilmentType.InStock)
        {
            if (stockQuantity is null or < 0)
            {
                errors.Add("stockQuantity", "In-stock variants need a stock quantity of 0 or more.");
            }
        }
        else if (stockQuantity is not null)
        {
            errors.Add("stockQuantity", "Made-to-order variants have no stock quantity.");
        }

        if (attributes is not null)
        {
            if (attributes.Count > MaxAttributes)
            {
                errors.Add("attributes", $"A variant has at most {MaxAttributes} attributes.");
            }

            foreach (var (key, value) in attributes)
            {
                if (string.IsNullOrWhiteSpace(key) || key.Length > AttributeKeyMaxLength)
                {
                    errors.Add("attributes", $"An attribute name is required and is at most {AttributeKeyMaxLength} characters.");
                }
                else if (value is null || value.Length > AttributeValueMaxLength)
                {
                    errors.Add("attributes", $"The value of '{key}' is required and is at most {AttributeValueMaxLength} characters.");
                }
            }
        }

        return errors;
    }
}
