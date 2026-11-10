namespace Storefront.Application.Contracts;

/// <summary>Where the files of product images live. The database keeps only the key, never the file.</summary>
public interface IImageStorage
{
    /// <summary>Stores the file under the key, replacing any file that already has it.</summary>
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the file. A file that is already gone is not an error. Any other failure is written to the log by the
    /// implementation and not thrown: a file left behind is harmless, a failed request because of it is not.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
