using Storefront.Domain.Catalog;

namespace Storefront.Application.Catalog;

/// <summary>
/// The fields that adding and editing a variant have in common. One place checks them (<see cref="VariantRules"/>)
/// and one place writes them to the variant (<see cref="ApplyTo"/>), so the two operations cannot drift apart.
/// </summary>
internal sealed record VariantFields(
    string Sku,
    string Name,
    long Price,
    int WeightGrams,
    long? CompareAtPrice,
    int? StockQuantity,
    IReadOnlyDictionary<string, string>? Attributes)
{
    public string TrimmedSku => Sku.Trim();

    public static VariantFields From(NewVariant input) =>
        new(
            input.Sku,
            input.Name,
            input.Price,
            input.WeightGrams,
            input.CompareAtPrice,
            input.StockQuantity,
            input.Attributes);

    public static VariantFields From(UpdateVariant input) =>
        new(
            input.Sku,
            input.Name,
            input.Price,
            input.WeightGrams,
            input.CompareAtPrice,
            input.StockQuantity,
            input.Attributes);

    /// <summary>Writes the fields to the variant. Call it only after <see cref="VariantRules.Validate"/> accepted them.</summary>
    public void ApplyTo(ProductVariant variant)
    {
        variant.Sku = TrimmedSku;
        variant.Name = Name.Trim();
        variant.Price = Price;
        variant.CompareAtPrice = CompareAtPrice;
        variant.StockQuantity = StockQuantity;
        variant.WeightGrams = WeightGrams;
        variant.Attributes = Attributes is null ? [] : new Dictionary<string, string>(Attributes);
    }
}
