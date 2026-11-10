using Storefront.Application.Exceptions;

namespace Storefront.Application.Catalog;

/// <summary>The rules for an uploaded product image. No database and no storage.</summary>
internal static class ImageRules
{
    public const int MaxBytes = ImageLimits.MaxBytes;
    public const int MaxImagesPerProduct = ImageLimits.MaxImagesPerProduct;
    public const int AltTextMaxLength = ImageLimits.AltTextMaxLength;

    public const string TypeNotAllowedCode = "image.type_not_allowed";
    public const string TooLargeCode = "image.too_large";
    public const string EmptyCode = "image.empty";
    public const string LimitReachedCode = "image.limit_reached";
    public const string NotFoundCode = "image.not_found";

    /// <summary>The file types that are allowed. The type is decided from the first bytes of the file, never from its name or header.</summary>
    public sealed record ImageType(string Extension, string ContentType);

    private static readonly ImageType Jpeg = new("jpg", "image/jpeg");
    private static readonly ImageType Png = new("png", "image/png");
    private static readonly ImageType Webp = new("webp", "image/webp");

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Reads at most <see cref="MaxBytes"/> + 1 bytes: one more than the limit is enough to know the file is too big.</summary>
    public static async Task<byte[]> ReadLimitedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (buffer.Length <= MaxBytes)
        {
            var wanted = (int)Math.Min(chunk.Length, MaxBytes + 1 - buffer.Length);
            var read = await content.ReadAsync(chunk.AsMemory(0, wanted), cancellationToken);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Checks the size and the type of the file and returns the type.</summary>
    public static ImageType CheckFile(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new FileRejectedException("The file is empty.", EmptyCode);
        }

        if (bytes.Length > MaxBytes)
        {
            throw new FileRejectedException(
                $"The file is larger than {MaxBytes / (1024 * 1024)} MB.", TooLargeCode, tooLarge: true);
        }

        return Detect(bytes)
            ?? throw new FileRejectedException("Only JPEG, PNG and WebP images are allowed.", TypeNotAllowedCode);
    }

    private static ImageType? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return Jpeg;
        }

        if (bytes.StartsWith(PngSignature))
        {
            return Png;
        }

        // WebP: "RIFF", four bytes of size, then "WEBP".
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        return null;
    }

    /// <summary>The key is made on the server from ids and a type: nothing the client sends reaches it.</summary>
    public static string NewKey(Guid productId, ImageType type) =>
        $"products/{productId:D}/{Guid.NewGuid():N}.{type.Extension}";

    public static string? AltTextError(string? altText)
    {
        var trimmed = altText?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "The alt text is required.";
        }

        return trimmed.Length > AltTextMaxLength
            ? $"The alt text must be at most {AltTextMaxLength} characters."
            : null;
    }
}
