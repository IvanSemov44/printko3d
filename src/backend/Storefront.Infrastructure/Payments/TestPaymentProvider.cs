using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Storefront.Application.Exceptions;
using Storefront.Application.Payments;

namespace Storefront.Infrastructure.Payments;

/// <summary>
/// The test provider (decision 0035): no outside service, no card fields and no card data. A message is signed with
/// HMAC-SHA256 over the time and the raw body, the way a real provider signs; the page "Test payment" asks the
/// simulator for a signed message and sends it to the same address a real provider would use.
/// </summary>
public sealed class TestPaymentProvider : IPaymentProvider, IPaymentSimulator
{
    public const string ProviderCode = "test";

    private const string Prefix = "test";

    private readonly byte[] _key;
    private readonly TimeSpan _tolerance;
    private readonly TimeProvider _time;

    public TestPaymentProvider(IOptions<TestPaymentOptions> options, TimeProvider time)
    {
        var settings = options.Value;
        _key = settings.Secret.Length > 0 ? Encoding.UTF8.GetBytes(settings.Secret) : RandomNumberGenerator.GetBytes(32);
        _tolerance = TimeSpan.FromSeconds(settings.ToleranceSeconds);
        _time = time;
    }

    public string Code => ProviderCode;

    public Task<PaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken cancellationToken)
    {
        // The reference carries the last moment to pay, so the provider needs no memory to refuse a late payment.
        var reference = string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}_{request.ExpiresAt.ToUnixTimeSeconds()}_{Guid.NewGuid():N}");
        return Task.FromResult(new PaymentSession(reference, $"/pay/test/{reference}"));
    }

    public Task<PaymentNotification> ReadNotificationAsync(PaymentMessage message, CancellationToken cancellationToken)
    {
        VerifySignature(message);
        return Task.FromResult(Parse(message.Body));
    }

    public Task<PaymentMessage> CreateMessageAsync(
        string reference,
        PaymentOutcome outcome,
        long amount,
        string? eventId,
        CancellationToken cancellationToken)
    {
        if (outcome == PaymentOutcome.Paid && ExpiryOf(reference) is { } expiresAt && _time.GetUtcNow() >= expiresAt)
        {
            throw new RuleViolationException("The payment session has expired.", "payment.session_expired");
        }

        var body = JsonSerializer.Serialize(new
        {
            eventId = eventId ?? Guid.NewGuid().ToString("N"),
            reference,
            outcome = outcome == PaymentOutcome.Paid ? "paid" : "failed",
            amount,
        });
        var timestamp = _time.GetUtcNow().ToUnixTimeSeconds();
        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"t={timestamp},v1={Convert.ToHexStringLower(Sign(timestamp, body))}");
        return Task.FromResult(new PaymentMessage(body, signature));
    }

    private static DateTimeOffset? ExpiryOf(string reference)
    {
        var parts = reference.Split('_');
        return parts.Length == 3
            && parts[0] == Prefix
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }

    private byte[] Sign(long timestamp, string body) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{timestamp}.{body}")));

    private void VerifySignature(PaymentMessage message)
    {
        if (!TryReadHeader(message.Signature, out var timestamp, out var provided))
        {
            throw new PaymentMessageRejectedException("The signature is missing or not in the expected form.");
        }

        var age = _time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (age.Duration() > _tolerance)
        {
            throw new PaymentMessageRejectedException("The signature is too old or from the future.");
        }

        if (!CryptographicOperations.FixedTimeEquals(Sign(timestamp, message.Body), provided))
        {
            throw new PaymentMessageRejectedException("The signature does not match.");
        }
    }

    // The header is "t=<unix seconds>,v1=<hex>". Anything else is refused without saying what was wrong.
    private static bool TryReadHeader(string? header, out long timestamp, out byte[] signature)
    {
        timestamp = 0;
        signature = [];
        var parts = header?.Split(',');
        if (parts is not { Length: 2 }
            || !parts[0].StartsWith("t=", StringComparison.Ordinal)
            || !parts[1].StartsWith("v1=", StringComparison.Ordinal)
            || !long.TryParse(parts[0].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out timestamp)
            || timestamp > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return false;
        }

        try
        {
            signature = Convert.FromHexString(parts[1].AsSpan(3));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static PaymentNotification Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var outcome = root.GetProperty("outcome").GetString() switch
            {
                "paid" => PaymentOutcome.Paid,
                "failed" => PaymentOutcome.Failed,
                _ => throw new PaymentMessageRejectedException("The outcome is not known."),
            };
            var eventId = root.GetProperty("eventId").GetString();
            var reference = root.GetProperty("reference").GetString();
            return string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(reference)
                ? throw new PaymentMessageRejectedException("The event number or the reference is empty.")
                : new PaymentNotification(eventId, reference, outcome, root.GetProperty("amount").GetInt64());
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new PaymentMessageRejectedException("The body is not in the expected form.");
        }
    }
}
