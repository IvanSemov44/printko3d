using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Storefront.Application.Contracts;
using Storefront.Application.Health;
using Storefront.Infrastructure.Health;
using Storefront.Infrastructure.Persistence;
using Storefront.Infrastructure.Repositories;

namespace Storefront.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<StorefrontDbContext>((serviceProvider, options) => options.UseNpgsql(
            serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IRepositoryManager, RepositoryManager>();
        services.AddScoped<IDatabaseProbe, NpgsqlDatabaseProbe>();
        return services;
    }
}
