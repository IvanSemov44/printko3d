using System.ComponentModel.DataAnnotations;
using Storefront.Application.Catalog;

namespace Storefront.Api.Requests;

/// <summary>The form that uploads one image. The name of the file is ignored: the server makes the storage key itself.</summary>
/// <param name="File">JPEG, PNG or WebP, at most 5 MB. The type is decided from the content of the file.</param>
/// <param name="AltText">What the image shows, 1 to 300 characters.</param>
/// <param name="VariantId">A variant of the same product that the image shows. Empty for the whole product.</param>
public sealed record UploadImageRequest(
    [Required] IFormFile File,
    [Required] string AltText,
    Guid? VariantId)
{
    public NewImageUpload ToModel(Stream content) => new(content, AltText, VariantId);
}
