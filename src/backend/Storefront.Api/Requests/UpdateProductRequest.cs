using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Storefront.Application.Catalog;
using Storefront.Domain.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The body of the request that replaces the editable fields of a product (not the images, status or variants).</summary>
/// <param name="Version">The version the form was loaded with. A different value means someone else saved first.</param>
/// <param name="VariantStock">
/// The stock of every variant, by variant id. Required only when the product changes from MadeToOrder to InStock.
/// </param>
public sealed record UpdateProductRequest(
    Guid CategoryId,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(200)] string Slug,
    [Required(AllowEmptyStrings = true), StringLength(Product.ShortDescriptionMaxLength)] string ShortDescription,
    [Required(AllowEmptyStrings = true)] string Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] FulfilmentType FulfilmentType,
    int? LeadTimeDays,
    [StringLength(200)] string? SeoTitle,
    [StringLength(500)] string? SeoDescription,
    uint Version,
    Dictionary<Guid, int>? VariantStock = null)
{
    public UpdateProduct ToModel() =>
        new(
            CategoryId,
            Name,
            Slug,
            ShortDescription,
            Description,
            FulfilmentType,
            LeadTimeDays,
            SeoTitle,
            SeoDescription,
            Version,
            VariantStock);
}
