using Storefront.Application.Catalog;
using Storefront.Application.Health;
using Storefront.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<HealthService>();
builder.Services.AddScoped<IServiceManager, ServiceManager>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods(HttpMethods.Get)
        .AllowAnyHeader()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", async (HealthService healthService, CancellationToken cancellationToken) =>
{
    var status = await healthService.CheckAsync(cancellationToken);
    return status.IsHealthy
        ? Results.Ok(status)
        : Results.Problem(
            title: "Service unavailable",
            detail: "The database is not reachable.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
})
.WithName("GetHealth")
.Produces<HealthStatus>()
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.Run();

public partial class Program;
