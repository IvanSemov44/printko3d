using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Storefront.IntegrationTests;

public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:ConnectionString", connectionString);

        // The sender of queued emails must not run by itself in the middle of a test: the tests call the service when they want it.
        builder.UseSetting("EmailOutbox:InitialDelaySeconds", "3600");
    }
}
