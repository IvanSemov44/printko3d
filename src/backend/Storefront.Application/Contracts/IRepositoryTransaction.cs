namespace Storefront.Application.Contracts;

/// <summary>
/// One database transaction. Disposing it without <see cref="CommitAsync"/> rolls everything back, so a refusal in
/// the middle of a service call leaves nothing behind.
/// </summary>
public interface IRepositoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
