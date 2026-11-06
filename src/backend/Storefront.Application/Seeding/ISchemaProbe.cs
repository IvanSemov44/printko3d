namespace Storefront.Application.Seeding;

public interface ISchemaProbe
{
    /// <summary>True when the database is reachable and every migration is applied.</summary>
    Task<bool> IsUpToDateAsync(CancellationToken cancellationToken);
}
