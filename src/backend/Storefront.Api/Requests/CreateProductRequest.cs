using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Storefront.Application.Catalog;
using Storefront.Domain.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that creates a product. It starts as a draft without variants; images come with a later task.</summary>
/// <param name="FulfilmentType">InStock or MadeToOrder.</param>
/// <param name="LeadTimeDays">Required for MadeToOrder (at least 1). Ignored for InStock.</param>
public sealed record CreateProductRequest(
    Guid CategoryId,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(200)] string Slug,
    [Required(AllowEmptyStrings = true), StringLength(Product.ShortDescriptionMaxLength)] string ShortDescription,
    [Required(AllowEmptyStrings = true)] string Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    [StringLength(200)] string? SeoTitle,
    [StringLength(500)] string? SeoDescription)
{
    public NewProduct ToModel() =>
        new(CategoryId, Name, Slug, ShortDescription, Description, FulfilmentType, LeadTimeDays, SeoTitle, SeoDescription);
}
