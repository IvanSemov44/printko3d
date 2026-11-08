using Npgsql;

namespace Storefront.Infrastructure;

/// <summary>
/// Turns a `postgres://user:password@host:port/database` address (the form hosting platforms such as
/// render.com hand out) into an Npgsql connection string.
/// </summary>
public static class PostgresConnectionString
{
    private const int DefaultPort = 5432;

    public static string FromUrl(string url)
    {
        // The address carries the password, so no message below repeats it.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql")
            || uri.Host.Length == 0)
        {
            throw new FormatException("The database address must look like postgres://user:password@host:port/database.");
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        if (credentials[0].Length == 0 || database.Length == 0)
        {
            throw new FormatException("The database address needs a user name and a database name.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port < 0 ? DefaultPort : uri.Port,
            Database = database,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            GssEncryptionMode = GssEncryptionMode.Disable,
        }.ConnectionString;
    }
}
