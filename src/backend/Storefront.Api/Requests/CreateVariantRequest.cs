using System.ComponentModel.DataAnnotations;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that adds a variant to a product.</summary>
/// <param name="Price">In the smallest currency unit (for example stotinki), as a whole number.</param>
/// <param name="StockQuantity">Required for an InStock product, and must be empty for a MadeToOrder one.</param>
/// <param name="Attributes">Up to 10 name and value pairs, for example color and size.</param>
public sealed record CreateVariantRequest(
    [Required, StringLength(100)] string Sku,
    [Required, StringLength(200)] string Name,
    long Price,
    int WeightGrams,
    long? CompareAtPrice,
    int? StockQuantity,
    Dictionary<string, string>? Attributes,
    bool IsActive = true,
    int SortOrder = 0)
{
    public NewVariant ToModel() =>
        new(Sku, Name, Price, WeightGrams, CompareAtPrice, StockQuantity, Attributes, IsActive, SortOrder);
}
