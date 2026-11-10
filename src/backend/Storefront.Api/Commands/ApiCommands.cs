namespace Storefront.Api.Commands;

/// <summary>
/// The command line commands of the API (`migrate`, `seed`, `create-admin`). Each one runs, writes its result to
/// the console and exits without starting a web server.
/// </summary>
internal static class ApiCommands
{
    /// <summary>The exit code of the command named by <paramref name="args"/>, or null when no command was asked for.</summary>
    public static async Task<int?> TryRunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (MigrateCommand.IsRequested(args))
        {
            return await MigrateCommand.RunAsync(args, Console.Out, Console.Error, cancellationToken);
        }

        if (SeedCommand.IsRequested(args))
        {
            return await SeedCommand.RunAsync(args, Console.Out, Console.Error, cancellationToken);
        }

        if (CreateAdminCommand.IsRequested(args))
        {
            return await CreateAdminCommand.RunAsync(args, Console.Out, Console.Error, cancellationToken);
        }

        return null;
    }
}
