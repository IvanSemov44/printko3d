using Storefront.Api;
using Storefront.Application.Catalog;
using Storefront.Application.Health;
using Storefront.Infrastructure;

if (MigrateCommand.IsRequested(args))
{
    Environment.ExitCode = await MigrateCommand.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
    return;
}

if (SeedCommand.IsRequested(args))
{
    Environment.ExitCode = await SeedCommand.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
    return;
}

if (CreateAdminCommand.IsRequested(args))
{
    Environment.ExitCode = await CreateAdminCommand.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // Model-binding errors (for example page=abc) never reach ApiExceptionHandler; give them the same stable code.
    if (context.ProblemDetails.Status == StatusCodes.Status400BadRequest)
    {
        context.ProblemDetails.Extensions.TryAdd("code", "validation.failed");
    }
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);
builder.AddAdminAuth();
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
.AllowAnonymous()
.WithName("GetHealth")
.Produces<HealthStatus>()
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.Run();

public partial class Program;
