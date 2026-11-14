namespace Storefront.Application.Payments;

/// <summary>The API refused to start with the test payment because of the environment.</summary>
public sealed class PaymentTestRefusedException(string message) : Exception(message);
