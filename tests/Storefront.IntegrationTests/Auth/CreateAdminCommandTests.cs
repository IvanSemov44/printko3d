using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Storefront.Api.Commands;
using Storefront.Application.Auth;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class CreateAdminCommandTests(PostgresFixture postgres)
{
    private const string Email = "owner@example.test";

    // A made-up password for tests only.
    private const string Password = "a made-up long password";

    private sealed record Outcome(int ExitCode, string Output, string Error);

    private static async Task<Outcome> RunAsync(
        TestDatabase database,
        string? email = Email,
        string? password = Password,
        string? environment = "Development")
    {
        var args = new List<string> { CreateAdminCommand.Name, $"--Database:ConnectionString={database.ConnectionString}" };
        if (email is not null)
        {
            args.Add($"--Admin:Email={email}");
        }

        if (password is not null)
        {
            args.Add($"--Admin:Password={password}");
        }

        if (environment is not null)
        {
            args.Add($"--environment={environment}");
        }

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CreateAdminCommand.RunAsync([.. args], output, error, default);
        return new Outcome(exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task CreateAdmin_CreatesAnAdministratorWhoCanSignIn()
    {
        var database = await postgres.CreateDatabaseAsync();

        var outcome = await RunAsync(database);

        Assert.Equal(0, outcome.ExitCode);
        await using var read = database.CreateContext();
        var user = await read.Users.SingleAsync();
        Assert.Equal(Email, user.Email);
        var adminRole = await read.Roles.SingleAsync(r => r.Name == AdminRole.Name);
        Assert.True(await read.UserRoles.AnyAsync(r => r.UserId == user.Id && r.RoleId == adminRole.Id));
        // The password is stored only as a hash made by Identity.
        Assert.DoesNotContain(Password, user.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<object>().VerifyHashedPassword(new object(), user.PasswordHash!, Password));
    }

    [Fact]
    public async Task CreateAdmin_RunTwice_DoesNotDuplicateAndKeepsTheFirstPassword()
    {
        var database = await postgres.CreateDatabaseAsync();
        await RunAsync(database);

        var second = await RunAsync(database, password: "some other made-up password");

        Assert.Equal(0, second.ExitCode);
        await using var read = database.CreateContext();
        var user = await read.Users.SingleAsync();
        Assert.Equal(1, await read.Roles.CountAsync());
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<object>().VerifyHashedPassword(new object(), user.PasswordHash!, Password));
    }

    [Fact]
    public async Task CreateAdmin_WithoutAPassword_IsRefusedAndCreatesNothing()
    {
        var database = await postgres.CreateDatabaseAsync();

        var outcome = await RunAsync(database, password: null);

        Assert.Equal(1, outcome.ExitCode);
        await using var read = database.CreateContext();
        Assert.Empty(await read.Users.ToListAsync());
    }

    [Fact]
    public async Task CreateAdmin_WithoutAnEmail_IsRefused()
    {
        var database = await postgres.CreateDatabaseAsync();

        var outcome = await RunAsync(database, email: null);

        Assert.Equal(1, outcome.ExitCode);
    }

    [Fact]
    public async Task CreateAdmin_WithATooShortPassword_IsRefused()
    {
        var database = await postgres.CreateDatabaseAsync();

        var outcome = await RunAsync(database, password: "short");

        Assert.Equal(1, outcome.ExitCode);
        Assert.Contains("PasswordTooShort", outcome.Error, StringComparison.Ordinal);
        await using var read = database.CreateContext();
        Assert.Empty(await read.Users.ToListAsync());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData(null)]
    public async Task CreateAdmin_OutsideDevelopmentAndDemo_IsRefused(string? environment)
    {
        var database = await postgres.CreateDatabaseAsync();

        // Without the argument the host environment is Production.
        var outcome = await RunAsync(database, environment: environment);

        Assert.Equal(1, outcome.ExitCode);
        await using var read = database.CreateContext();
        Assert.Empty(await read.Users.ToListAsync());
    }

    [Fact]
    public async Task CreateAdmin_NeverPrintsThePasswordOrTheEmail()
    {
        var database = await postgres.CreateDatabaseAsync();

        var created = await RunAsync(database);
        var weak = await RunAsync(database, email: "second@example.test", password: "short");

        foreach (var text in new[] { created.Output, created.Error, weak.Output, weak.Error })
        {
            Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
            Assert.DoesNotContain("short", text.Replace("PasswordTooShort", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.DoesNotContain("example.test", text, StringComparison.Ordinal);
        }
    }
}
