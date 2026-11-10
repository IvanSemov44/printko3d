namespace Storefront.Infrastructure.Storage;

/// <summary>
/// The S3-compatible bucket that holds product images (decision 0018). Every value comes from the environment
/// (for example ImageStorage__SecretKey); nothing is read from a file in the repository.
/// </summary>
public sealed class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";

    /// <summary>The S3 address of the service, for example https://project.storage.supabase.co/storage/v1/s3.</summary>
    public string Endpoint { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Bucket { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;

    /// <summary>A secret. It must never be logged or returned.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>The names of the settings that are empty. Only names, never values.</summary>
    public IReadOnlyList<string> MissingSettings() =>
        new (string Name, string Value)[]
        {
            (nameof(Endpoint), Endpoint),
            (nameof(Region), Region),
            (nameof(Bucket), Bucket),
            (nameof(AccessKey), AccessKey),
            (nameof(SecretKey), SecretKey),
        }
        .Where(setting => string.IsNullOrWhiteSpace(setting.Value))
        .Select(setting => $"{SectionName}:{setting.Name}")
        .ToList();
}
