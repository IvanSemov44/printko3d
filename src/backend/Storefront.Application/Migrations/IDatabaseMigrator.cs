namespace Storefront.Application.Migrations;

public interface IDatabaseMigrator
{
    /// <summary>Applies every pending migration and returns their names, in the order they were applied.</summary>
    Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken);
}
