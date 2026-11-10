using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Storefront.Application.Contracts;
using Storefront.Application.Health;
using Storefront.Application.Migrations;
using Storefront.Application.Seeding;
using Storefront.Infrastructure.Health;
using Storefront.Infrastructure.Persistence;
using Storefront.Infrastructure.Repositories;
using Storefront.Infrastructure.Storage;

namespace Storefront.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ConnectionString) && !string.IsNullOrWhiteSpace(options.Url))
                {
                    options.ConnectionString = PostgresConnectionString.FromUrl(options.Url);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        // Not validated at startup: the API runs without image storage, and only the image calls fail until it is set.
        services.AddOptions<ImageStorageOptions>().Bind(configuration.GetSection(ImageStorageOptions.SectionName));
        services.AddSingleton<IImageStorage, S3ImageStorage>();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<StorefrontDbContext>((serviceProvider, options) => options.UseNpgsql(
            serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IRepositoryManager, RepositoryManager>();
        services.AddScoped<IDatabaseProbe, NpgsqlDatabaseProbe>();
        services.AddScoped<ISchemaProbe, MigrationSchemaProbe>();
        services.AddScoped<IDatabaseMigrator, EfDatabaseMigrator>();
        return services;
    }
}
