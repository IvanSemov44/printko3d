namespace Storefront.Application.Catalog;

/// <summary>The limits of product images, public so the API can size its request limit from the same number.</summary>
public static class ImageLimits
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxImagesPerProduct = 20;
    public const int AltTextMaxLength = 300;
}
