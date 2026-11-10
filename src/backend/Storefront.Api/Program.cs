using Storefront.Api.Commands;
using Storefront.Api.Setup;

if (await ApiCommands.TryRunAsync(args, CancellationToken.None) is { } exitCode)
{
    Environment.ExitCode = exitCode;
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddApiServices();

var app = builder.Build();
app.UseApiPipeline();
app.Run();

public partial class Program;
