using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storefront.Application.Seeding;

namespace Storefront.Infrastructure.Persistence;

internal sealed class MigrationSchemaProbe(StorefrontDbContext context) : ISchemaProbe
{
    public async Task<bool> IsUpToDateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            return applied.Any() && !pending.Any();
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }
}
