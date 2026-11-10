using System.ComponentModel.DataAnnotations;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>Replaces the alt text and the variant of an image.</summary>
/// <param name="AltText">What the image shows, 1 to 300 characters.</param>
/// <param name="VariantId">A variant of the same product, or empty for the whole product.</param>
public sealed record UpdateImageRequest([Required] string AltText, Guid? VariantId)
{
    public UpdateImage ToModel() => new(AltText, VariantId);
}
