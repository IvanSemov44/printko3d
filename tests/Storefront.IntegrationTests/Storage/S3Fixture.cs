using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Storefront.IntegrationTests.Storage;

/// <summary>
/// A throw-away S3 service in a container (Adobe S3Mock, Apache 2.0), so the real S3 code runs in tests without
/// internet and without any key. The credentials below are made up and open nothing.
/// </summary>
public sealed class S3Fixture : IAsyncLifetime
{
    public const string Bucket = "test-images";
    public const string AccessKey = "test-access-key";
    public const string SecretKey = "test-secret-key";
    public const string Region = "eu-central-1";

    private const int Port = 9090;

    private readonly IContainer _container = new ContainerBuilder("adobe/s3mock:4.7.0")
        .WithPortBinding(Port, true)
        .WithEnvironment("initialBuckets", Bucket)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(Port)
            // Listing the buckets answers 200 once the service is up (its health address wants a special Accept header).
            .ForPath("/")))
        .Build();

    public string Endpoint => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(Port)}";

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>The settings the API reads, as if they came from the environment.</summary>
    public Dictionary<string, string> Settings() => new()
    {
        ["ImageStorage:Endpoint"] = Endpoint,
        ["ImageStorage:Region"] = Region,
        ["ImageStorage:Bucket"] = Bucket,
        ["ImageStorage:AccessKey"] = AccessKey,
        ["ImageStorage:SecretKey"] = SecretKey,
    };

    /// <summary>A client for the tests to look into the bucket, independent of the code under test.</summary>
    public AmazonS3Client CreateClient() => new(
        new BasicAWSCredentials(AccessKey, SecretKey),
        new AmazonS3Config { ServiceURL = Endpoint, ForcePathStyle = true, AuthenticationRegion = Region });

    public async Task<IReadOnlyList<string>> KeysAsync(string prefix = "")
    {
        using var client = CreateClient();
        var response = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, Prefix = prefix });
        return (response.S3Objects ?? []).Select(o => o.Key).Order().ToList();
    }
}

[CollectionDefinition(Name)]
public sealed class S3Collection : ICollectionFixture<S3Fixture>
{
    public const string Name = "s3";
}
