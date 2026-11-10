using System.ComponentModel.DataAnnotations;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that replaces the editable fields of a variant. Active state and order have their own requests.</summary>
public sealed record UpdateVariantRequest(
    [Required, StringLength(100)] string Sku,
    [Required, StringLength(200)] string Name,
    long Price,
    int WeightGrams,
    long? CompareAtPrice,
    int? StockQuantity,
    Dictionary<string, string>? Attributes)
{
    public UpdateVariant ToModel() =>
        new(Sku, Name, Price, WeightGrams, CompareAtPrice, StockQuantity, Attributes);
}
