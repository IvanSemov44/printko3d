using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Storefront.Infrastructure.Persistence;

// Used only by `dotnet ef`. It never connects when adding a migration.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StorefrontDbContext>
{
    public StorefrontDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString")
            ?? "Host=localhost;Database=storefront_design";
        var options = new DbContextOptionsBuilder<StorefrontDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new StorefrontDbContext(options, TimeProvider.System);
    }
}
