using Microsoft.EntityFrameworkCore;
using Storefront.Application.Migrations;

namespace Storefront.Infrastructure.Persistence;

internal sealed class EfDatabaseMigrator(StorefrontDbContext context) : IDatabaseMigrator
{
    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        return pending;
    }
}
