using Storefront.Application.Catalog;
using Storefront.Application.Seeding;
using Storefront.Infrastructure;

namespace Storefront.Api;

/// <summary>
/// `Storefront.Api seed`: loads the demo data and exits. It starts no web server and applies no migrations.
/// Any further arguments are normal configuration arguments (for example `--environment=Demo`).
/// </summary>
public static class SeedCommand
{
    public const string Name = "seed";

    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Name, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddScoped<IServiceManager, ServiceManager>();
        builder.Services.AddScoped<DemoDataSeeder>();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        try
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            var result = await seeder.SeedAsync(app.Environment.EnvironmentName, cancellationToken);
            await output.WriteLineAsync(
                $"Seed finished in '{app.Environment.EnvironmentName}': "
                + $"{result.CategoriesCreated} categories and {result.ProductsCreated} products created, "
                + $"{result.ProductsSkipped} products already existed, "
                + $"settings {(result.SettingsCreated ? "created" : "already existed")}.");
            return 0;
        }
        catch (SeedRefusedException exception)
        {
            await error.WriteLineAsync($"Seed refused: {exception.Message}");
            return 1;
        }
    }
}
