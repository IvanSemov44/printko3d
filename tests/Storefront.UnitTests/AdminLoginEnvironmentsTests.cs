using Storefront.Application.Auth;

namespace Storefront.UnitTests;

public sealed class AdminLoginEnvironmentsTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Demo")]
    [InlineData("demo")]
    public void EnsureAllowed_InDevelopmentAndDemo_DoesNotThrow(string environment) =>
        AdminLoginEnvironments.EnsureAllowed(environment);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    [InlineData(null)]
    public void EnsureAllowed_InAnyOtherEnvironment_Refuses(string? environment)
    {
        var exception = Assert.Throws<AdminLoginRefusedException>(
            () => AdminLoginEnvironments.EnsureAllowed(environment));

        Assert.Contains("two-factor", exception.Message, StringComparison.Ordinal);
    }
}
