using Storefront.Api.Auth;
using Storefront.Api.Errors;
using Storefront.Application.Catalog;
using Storefront.Application.Health;
using Storefront.Infrastructure;

namespace Storefront.Api.Setup;

/// <summary>What the API needs at startup, in two steps: the services, then the request pipeline.</summary>
internal static class ApiSetup
{
    public static void AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // Model-binding errors (for example page=abc) never reach ApiExceptionHandler; give them the same stable code.
            if (context.ProblemDetails.Status == StatusCodes.Status400BadRequest)
            {
                context.ProblemDetails.Extensions.TryAdd("code", ApiProblems.ValidationCode);
            }
        });
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.AddAdminAuth();
        builder.Services.AddScoped<HealthService>();
        builder.Services.AddScoped<IServiceManager, ServiceManager>();
        builder.Services.AddApiCors(builder.Configuration);
        builder.Services
            .AddOptions<SiteOptions>()
            .Bind(builder.Configuration.GetSection(SiteOptions.SectionName))
            .Validate(SiteOptions.IsValid, "Site:Url must be an http(s) address without a path, for example https://shop.example.")
            .ValidateOnStart();
    }

    /// <summary>The order matters: errors first, then CORS, the limiter, the CSRF check, and who you are before what you may do.</summary>
    public static void UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        // Turns the empty 401, 403 and 429 answers into ProblemDetails.
        app.UseStatusCodePages();
        app.UseCors();
        app.UseRateLimiter();
        app.UseCsrfHeaderCheck();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.MapControllers();
    }

    /// <summary>Only the listed origins, and only GET: the panel and the shop reach the API through their own addresses.</summary>
    private static void AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(allowedOrigins)
                .WithMethods(HttpMethods.Get)
                .AllowAnyHeader()));
    }
}
