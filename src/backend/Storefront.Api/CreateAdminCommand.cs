using Storefront.Application.Auth;
using Storefront.Infrastructure;
using Storefront.Infrastructure.Identity;

namespace Storefront.Api;

/// <summary>
/// `Storefront.Api create-admin`: creates the administrator and exits. There is no registration.
/// The email comes from `Admin:Email` (environment variable `Admin__Email`). The password comes from
/// `Admin:Password` (`Admin__Password`) or, in a terminal, is typed without an echo. There is no default password,
/// and the password and the email are never printed or logged. Safe to repeat: an existing account is left as it is.
/// Works only in the environments that allow administrator login. It starts no web server and applies no migrations.
/// </summary>
public static class CreateAdminCommand
{
    public const string Name = "create-admin";

    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Name, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        try
        {
            AdminLoginEnvironments.EnsureAllowed(builder.Environment.EnvironmentName);
        }
        catch (AdminLoginRefusedException exception)
        {
            await error.WriteLineAsync($"Create-admin refused: {exception.Message}");
            return 1;
        }

        var email = builder.Configuration["Admin:Email"];
        if (string.IsNullOrWhiteSpace(email))
        {
            await error.WriteLineAsync("Create-admin refused: set Admin__Email (the email of the administrator).");
            return 1;
        }

        var password = builder.Configuration["Admin:Password"];
        if (string.IsNullOrEmpty(password) && !Console.IsInputRedirected)
        {
            password = ReadPassword(output);
        }

        if (string.IsNullOrEmpty(password))
        {
            await error.WriteLineAsync(
                "Create-admin refused: set Admin__Password, or run in a terminal to type it. There is no default password.");
            return 1;
        }

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddAdminIdentity();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        try
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAdminAccountService>();
            var result = await accounts.EnsureAdminAsync(email.Trim(), password, cancellationToken);
            await output.WriteLineAsync(result == EnsureAdminResult.Created
                ? "Create-admin finished: the administrator account was created."
                : "Create-admin finished: the administrator account already existed and was left as it is.");
            return 0;
        }
        catch (AdminAccountException exception)
        {
            await error.WriteLineAsync($"Create-admin refused: {exception.Message}");
            return 1;
        }
        catch (Npgsql.NpgsqlException)
        {
            // The message of a database exception can carry the host and user name; keep it out of the output.
            await error.WriteLineAsync(
                "Create-admin failed: the database is not reachable or the schema is missing (run migrate first).");
            return 1;
        }
    }

    private static string ReadPassword(TextWriter output)
    {
        output.Write("Password: ");
        var typed = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }

        output.WriteLine();
        return typed.ToString();
    }
}
