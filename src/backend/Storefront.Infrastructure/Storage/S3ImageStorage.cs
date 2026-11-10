using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storefront.Application.Contracts;

namespace Storefront.Infrastructure.Storage;

/// <summary>Keeps images in an S3-compatible bucket. The client is made on first use, so a missing setting stops only the image calls.</summary>
public sealed class S3ImageStorage(IOptions<ImageStorageOptions> options, ILogger<S3ImageStorage> logger)
    : IImageStorage, IDisposable
{
    private readonly Lazy<AmazonS3Client> _client = new(() => CreateClient(options.Value));

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await _client.Value.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.Value.Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                // Images never change under one key, so the shop and the CDN may keep them for a long time.
                Headers = { CacheControl = "public, max-age=31536000, immutable" },
                // Some S3-compatible services reject the streaming signature.
                UseChunkEncoding = false,
            },
            cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _client.Value.DeleteObjectAsync(options.Value.Bucket, key, cancellationToken);
        }
        catch (Exception exception) when (exception is AmazonServiceException or HttpRequestException or InvalidOperationException)
        {
            // Never the secret and never the response body: the key and the kind of failure are enough to clean up by hand.
            logger.LogWarning(
                "An image file could not be deleted and is left in the bucket. Key: {StorageKey}. Failure: {Failure}",
                key,
                exception.GetType().Name);
        }
    }

    public void Dispose()
    {
        if (_client.IsValueCreated)
        {
            _client.Value.Dispose();
        }
    }

    private static AmazonS3Client CreateClient(ImageStorageOptions settings)
    {
        if (settings.MissingSettings() is { Count: > 0 } missing)
        {
            throw new InvalidOperationException($"Image storage is not configured. Missing: {string.Join(", ", missing)}.");
        }

        return new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.Endpoint,
                AuthenticationRegion = settings.Region,
                ForcePathStyle = true,
                // Newer SDK versions add checksums that some S3-compatible services do not know.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }
}
