namespace Storefront.Infrastructure.Payments;

/// <summary>
/// Settings of the test payment provider. Bound from <c>Payment:Test</c>; every value comes from the environment
/// (for example <c>Payment__Test__Secret</c>), nothing is read from a file in the repository.
/// </summary>
public sealed class TestPaymentOptions
{
    public const string SectionName = "Payment:Test";

    /// <summary>
    /// The key that signs and checks the messages. A secret: it is never logged or returned. When it is empty a random
    /// key is made when the API starts, which is enough while one process signs and checks; set it to send a message by hand.
    /// </summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>How far the time of a message may be from ours, in seconds. A message outside is a replay.</summary>
    public int ToleranceSeconds { get; init; } = 300;
}
