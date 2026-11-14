using Microsoft.EntityFrameworkCore.Storage;
using Storefront.Application.Contracts;

namespace Storefront.Infrastructure.Repositories;

internal sealed class RepositoryTransaction(IDbContextTransaction transaction) : IRepositoryTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
