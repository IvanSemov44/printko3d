using System.ComponentModel.DataAnnotations;

namespace Storefront.Infrastructure;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>An Npgsql connection string. When empty, it is built from <see cref="Url"/>.</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>A `postgres://user:password@host:port/database` address, as hosting platforms provide it.</summary>
    public string Url { get; init; } = string.Empty;
}
