using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Storefront.Application.Exceptions;
using Storefront.Application.Payments;
using Storefront.Infrastructure.Payments;
using Storefront.IntegrationTests.Persistence;

namespace Storefront.IntegrationTests.Payments;

public sealed class TestPaymentProviderTests
{
    private const string Secret = "a-secret-for-tests-only";

    private static readonly DateTimeOffset Start = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static (TestPaymentProvider Provider, FakeTimeProvider Time) Create(string secret = Secret)
    {
        var time = new FakeTimeProvider(Start);
        var options = Options.Create(new TestPaymentOptions { Secret = secret });
        return (new TestPaymentProvider(options, time), time);
    }

    private static string Sign(string secret, long timestamp, string body) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"t={timestamp},v1={Convert.ToHexStringLower(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{timestamp}.{body}"))))}");

    private static PaymentMessage Message(string body, string secret = Secret, long? timestamp = null) =>
        new(body, Sign(secret, timestamp ?? Start.ToUnixTimeSeconds(), body));

    private const string PaidBody = "{\"eventId\":\"evt-1\",\"reference\":\"ref-1\",\"outcome\":\"paid\",\"amount\":3000}";

    [Fact]
    public async Task ReadNotification_ReadsAMessageWithAValidSignature()
    {
        var (provider, _) = Create();

        var notification = await provider.ReadNotificationAsync(Message(PaidBody), default);

        Assert.Equal(new PaymentNotification("evt-1", "ref-1", PaymentOutcome.Paid, 3000), notification);
    }

    [Fact]
    public async Task ReadNotification_ReadsAFailedPayment()
    {
        var (provider, _) = Create();
        var body = PaidBody.Replace("paid", "failed", StringComparison.Ordinal);

        var notification = await provider.ReadNotificationAsync(Message(body), default);

        Assert.Equal(PaymentOutcome.Failed, notification.Outcome);
    }

    [Fact]
    public async Task ReadNotification_RefusesAChangedBody()
    {
        var (provider, _) = Create();
        var signed = Message(PaidBody);

        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() =>
            provider.ReadNotificationAsync(signed with { Body = PaidBody.Replace("3000", "1", StringComparison.Ordinal) }, default));
    }

    [Fact]
    public async Task ReadNotification_RefusesASignatureMadeWithAnotherSecret()
    {
        var (provider, _) = Create();

        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() =>
            provider.ReadNotificationAsync(Message(PaidBody, secret: "another-secret"), default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abcd")]
    [InlineData("t=1,v1=not-hex")]
    [InlineData("t=abc,v1=00")]
    [InlineData("t=-5,v1=00")]
    [InlineData("t=99999999999999999999,v1=00")]
    [InlineData("t=1,v1=00,extra=1")]
    public async Task ReadNotification_RefusesAMissingOrMalformedSignature(string? header)
    {
        var (provider, _) = Create();

        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() =>
            provider.ReadNotificationAsync(new PaymentMessage(PaidBody, header), default));
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public async Task ReadNotification_RefusesAMessageOutsideTheTolerance_AsAReplay(int secondsFromNow)
    {
        var (provider, _) = Create();
        var message = Message(PaidBody, timestamp: Start.ToUnixTimeSeconds() + secondsFromNow);

        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() => provider.ReadNotificationAsync(message, default));
    }

    [Fact]
    public async Task ReadNotification_AcceptsAMessageAtTheEdgeOfTheTolerance()
    {
        var (provider, _) = Create();

        await provider.ReadNotificationAsync(Message(PaidBody, timestamp: Start.ToUnixTimeSeconds() - 300), default);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"eventId\":\"\",\"reference\":\"ref-1\",\"outcome\":\"paid\",\"amount\":1}")]
    [InlineData("{\"eventId\":\"e\",\"reference\":\"ref-1\",\"outcome\":\"refunded\",\"amount\":1}")]
    [InlineData("{\"eventId\":\"e\",\"reference\":\"ref-1\",\"outcome\":\"paid\",\"amount\":\"x\"}")]
    [InlineData("{\"eventId\":1,\"reference\":\"ref-1\",\"outcome\":\"paid\",\"amount\":1}")]
    public async Task ReadNotification_RefusesASignedBodyThatIsNotInTheExpectedForm(string body)
    {
        var (provider, _) = Create();

        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() => provider.ReadNotificationAsync(Message(body), default));
    }

    [Fact]
    public async Task ReadNotification_NeverPutsTheBodyOrTheSignatureInTheMessageOfTheException()
    {
        var (provider, _) = Create();
        var message = Message(PaidBody) with { Signature = "t=1,v1=00" };

        var exception = await Assert.ThrowsAsync<PaymentMessageRejectedException>(() => provider.ReadNotificationAsync(message, default));

        Assert.DoesNotContain("ref-1", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("v1=00", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMessage_IsReadByTheSameProvider()
    {
        var (provider, _) = Create();
        var session = await provider.CreateSessionAsync(new PaymentSessionRequest(12345678, 3000, "BGN", Start.AddHours(1)), default);

        var message = await provider.CreateMessageAsync(session.Reference, PaymentOutcome.Paid, 3000, "evt-9", default);
        var notification = await provider.ReadNotificationAsync(message, default);

        Assert.Equal(new PaymentNotification("evt-9", session.Reference, PaymentOutcome.Paid, 3000), notification);
    }

    [Fact]
    public async Task CreateMessage_WithoutAnEventNumber_MakesADifferentOneEachTime()
    {
        var (provider, _) = Create();

        var first = await provider.ReadNotificationAsync(await provider.CreateMessageAsync("ref", PaymentOutcome.Paid, 1, null, default), default);
        var second = await provider.ReadNotificationAsync(await provider.CreateMessageAsync("ref", PaymentOutcome.Paid, 1, null, default), default);

        Assert.NotEqual(first.EventId, second.EventId);
    }

    [Fact]
    public async Task CreateSession_ReturnsAReferenceAndAnAddressOfTheTestPage_WithoutTheBuyer()
    {
        var (provider, _) = Create();

        var session = await provider.CreateSessionAsync(new PaymentSessionRequest(12345678, 3000, "BGN", Start.AddHours(1)), default);

        Assert.Equal($"/pay/test/{session.Reference}", session.RedirectUrl);
        Assert.DoesNotContain("12345678", session.Reference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMessage_RefusesAPaymentAfterTheSessionExpired_ButStillAllowsAFailure()
    {
        var (provider, time) = Create();
        var session = await provider.CreateSessionAsync(new PaymentSessionRequest(12345678, 3000, "BGN", Start.AddMinutes(10)), default);
        time.Advance(TimeSpan.FromMinutes(10));

        var refused = await Assert.ThrowsAsync<RuleViolationException>(() =>
            provider.CreateMessageAsync(session.Reference, PaymentOutcome.Paid, 3000, null, default));
        var failure = await provider.CreateMessageAsync(session.Reference, PaymentOutcome.Failed, 3000, null, default);

        Assert.Equal("payment.session_expired", refused.Code);
        Assert.NotNull(failure.Signature);
    }

    [Fact]
    public async Task CreateMessage_JustBeforeTheExpiry_IsAllowed()
    {
        var (provider, time) = Create();
        var session = await provider.CreateSessionAsync(new PaymentSessionRequest(12345678, 3000, "BGN", Start.AddMinutes(10)), default);
        time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

        await provider.CreateMessageAsync(session.Reference, PaymentOutcome.Paid, 3000, null, default);
    }

    [Fact]
    public async Task WithoutASecret_ARandomKeyIsMade_SoOneProviderDoesNotReadAnotherOnesMessage()
    {
        var (first, _) = Create(secret: string.Empty);
        var (second, _) = Create(secret: string.Empty);
        var message = await first.CreateMessageAsync("ref", PaymentOutcome.Paid, 1, null, default);

        await first.ReadNotificationAsync(message, default);
        await Assert.ThrowsAsync<PaymentMessageRejectedException>(() => second.ReadNotificationAsync(message, default));
    }

    // The provider is taken from the real container, as the API does, so its registration is covered too.
    [Fact]
    public void TheContainer_GivesTheSameProviderForTheContractAndTheSimulator()
    {
        using var factory = new ApiFactory("Host=localhost;Database=unused");
        var provider = factory.Services.GetRequiredService<IPaymentProvider>();

        Assert.Same(provider, factory.Services.GetRequiredService<IPaymentSimulator>());
        Assert.Equal(TestPaymentProvider.ProviderCode, provider.Code);
        Assert.True(provider.IsTest);
    }
}
