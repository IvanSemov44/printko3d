using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Storefront.Infrastructure.Storage;

namespace Storefront.IntegrationTests.Storage;

[Collection(S3Collection.Name)]
public sealed class S3ImageStorageTests(S3Fixture s3)
{
    private static readonly byte[] Content = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private static string NewKey() => $"products/{Guid.NewGuid():D}/{Guid.NewGuid():N}.jpg";

    private S3ImageStorage CreateStorage(ImageStorageOptions? options = null) =>
        new(Options.Create(options ?? Configured()), NullLogger<S3ImageStorage>.Instance);

    private ImageStorageOptions Configured() => new()
    {
        Endpoint = s3.Endpoint,
        Region = S3Fixture.Region,
        Bucket = S3Fixture.Bucket,
        AccessKey = S3Fixture.AccessKey,
        SecretKey = S3Fixture.SecretKey,
    };

    [Fact]
    public async Task Save_WritesTheBytes_WithTheContentTypeAndALongCache()
    {
        using var storage = CreateStorage();
        var key = NewKey();

        await storage.SaveAsync(key, new MemoryStream(Content), "image/jpeg", default);

        using var client = s3.CreateClient();
        using var response = await client.GetObjectAsync(S3Fixture.Bucket, key);
        using var read = new MemoryStream();
        await response.ResponseStream.CopyToAsync(read);
        Assert.Equal(Content, read.ToArray());
        Assert.Equal("image/jpeg", response.Headers.ContentType);
        Assert.Contains("immutable", response.Headers.CacheControl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_RemovesTheObject()
    {
        using var storage = CreateStorage();
        var key = NewKey();
        await storage.SaveAsync(key, new MemoryStream(Content), "image/jpeg", default);

        await storage.DeleteAsync(key, default);

        Assert.DoesNotContain(key, await s3.KeysAsync("products/"));
    }

    [Fact]
    public async Task Delete_OfAFileThatIsAlreadyGone_IsNotAnError()
    {
        using var storage = CreateStorage();

        await storage.DeleteAsync(NewKey(), default);
    }

    [Fact]
    public async Task Delete_WhenTheServiceCannotBeReached_DoesNotThrow()
    {
        // Nothing listens on port 1, so the call fails. A file left behind must not fail the request that deleted its row.
        var unreachable = new ImageStorageOptions
        {
            Endpoint = "http://127.0.0.1:1",
            Region = S3Fixture.Region,
            Bucket = S3Fixture.Bucket,
            AccessKey = S3Fixture.AccessKey,
            SecretKey = S3Fixture.SecretKey,
        };
        using var storage = CreateStorage(unreachable);

        await storage.DeleteAsync(NewKey(), default);
    }

    [Fact]
    public async Task Save_WhenTheServiceCannotBeReached_Throws()
    {
        var unreachable = new ImageStorageOptions
        {
            Endpoint = "http://127.0.0.1:1",
            Region = S3Fixture.Region,
            Bucket = S3Fixture.Bucket,
            AccessKey = S3Fixture.AccessKey,
            SecretKey = S3Fixture.SecretKey,
        };
        using var storage = CreateStorage(unreachable);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => storage.SaveAsync(NewKey(), new MemoryStream(Content), "image/jpeg", default));
    }

    [Fact]
    public async Task Save_WithoutSettings_SaysWhichAreMissing_AndNeverTheirValues()
    {
        using var storage = CreateStorage(new ImageStorageOptions { SecretKey = "super-secret-value" });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.SaveAsync(NewKey(), new MemoryStream(Content), "image/jpeg", default));

        Assert.Contains("ImageStorage:Endpoint", error.Message, StringComparison.Ordinal);
        Assert.Contains("ImageStorage:Bucket", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageStorage:SecretKey", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-value", error.Message, StringComparison.Ordinal);
    }
}
