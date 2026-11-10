namespace Storefront.Application.Catalog;

/// <summary>An image file sent by the administrator. The name the client gave the file is not part of it on purpose.</summary>
public sealed record NewImageUpload(Stream Content, string AltText, Guid? VariantId = null);
