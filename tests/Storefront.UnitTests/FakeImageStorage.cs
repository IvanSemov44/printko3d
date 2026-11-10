using Storefront.Application.Contracts;

namespace Storefront.UnitTests;

/// <summary>Keeps the files in memory, so the service tests need neither a folder nor a network.</summary>
internal sealed class FakeImageStorage : IImageStorage
{
    public Dictionary<string, (byte[] Content, string ContentType)> Files { get; } = [];

    /// <summary>When set, every save fails with this exception.</summary>
    public Exception? SaveFailure { get; set; }

    public List<string> DeletedKeys { get; } = [];

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        using var copy = new MemoryStream();
        content.CopyTo(copy);
        Files[key] = (copy.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        DeletedKeys.Add(key);
        Files.Remove(key);
        return Task.CompletedTask;
    }
}
