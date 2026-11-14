using Storefront.Application.Payments;

namespace Storefront.UnitTests;

public sealed class PaymentTestEnvironmentsTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Demo")]
    [InlineData("demo")]
    public void EnsureAllowed_InDevelopmentAndDemo_DoesNotThrow(string environment) =>
        PaymentTestEnvironments.EnsureAllowed(environment);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    [InlineData(null)]
    public void EnsureAllowed_InAnyOtherEnvironment_Refuses(string? environment)
    {
        var exception = Assert.Throws<PaymentTestRefusedException>(
            () => PaymentTestEnvironments.EnsureAllowed(environment));

        Assert.Contains("real payment provider", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(60, 5, true)]
    [InlineData(1, 0, true)]
    [InlineData(0, 5, false)]
    [InlineData(-1, 5, false)]
    [InlineData(60, -1, false)]
    [InlineData(60 * 24 * 7 + 1, 5, false)]
    public void Options_AcceptOnlyAPositiveWindowAndANonNegativeGrace(int window, int grace, bool expected) =>
        Assert.Equal(
            expected,
            PaymentOptions.IsValid(new PaymentOptions { WindowMinutes = window, GraceMinutes = grace }));
}
