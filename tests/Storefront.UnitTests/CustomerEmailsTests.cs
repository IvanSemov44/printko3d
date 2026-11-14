using Storefront.Application.Customers;

namespace Storefront.UnitTests;

public sealed class CustomerEmailsTests
{
    private const string Shop = "https://shop.example/";

    [Fact]
    public void Confirmation_HasTheLinkWithTheEscapedAddressAndToken_AndNoUnfilledMark()
    {
        var message = CustomerEmails.Confirmation("a+b@example.test", "Buyer", Shop, "t/o+k=en", 90);

        Assert.Equal("a+b@example.test", message.To);
        Assert.Contains("https://shop.example/account/confirm-email?email=a%2Bb%40example.test&token=t%2Fo%2Bk%3Den", message.TextBody);
        Assert.Contains("90 минути", message.TextBody);
        Assert.Contains("Buyer", message.TextBody);
        Assert.DoesNotContain("{", message.TextBody);
        Assert.DoesNotContain("{", message.Subject);
    }

    [Fact]
    public void PasswordReset_PointsToTheResetPage()
    {
        var message = CustomerEmails.PasswordReset("a@example.test", "Buyer", Shop, "token", 120);

        Assert.Contains("https://shop.example/account/reset-password?email=a%40example.test&token=token", message.TextBody);
        Assert.DoesNotContain("{", message.TextBody);
    }

    [Fact]
    public void AlreadyRegistered_HasTheTwoLinks_AndNoToken()
    {
        var message = CustomerEmails.AlreadyRegistered("a@example.test", "Buyer", Shop);

        Assert.Contains("https://shop.example/account/login", message.TextBody);
        Assert.Contains("https://shop.example/account/forgot-password", message.TextBody);
        Assert.DoesNotContain("token", message.TextBody);
        Assert.DoesNotContain("{", message.TextBody);
    }

    [Theory]
    [InlineData(120, 60, 14, true)]
    [InlineData(4, 60, 14, false)]
    [InlineData(120, 0, 14, false)]
    [InlineData(120, 1001, 14, false)]
    [InlineData(120, 60, 0, false)]
    [InlineData(120, 60, 91, false)]
    public void CustomerOptions_AreValidOnlyInsideTheirLimits(int lifetime, int attempts, int days, bool valid)
    {
        var options = new CustomerOptions { TokenLifetimeMinutes = lifetime, AttemptsPerMinute = attempts, SessionDays = days };

        Assert.Equal(valid, CustomerOptions.IsValid(options));
    }
}
