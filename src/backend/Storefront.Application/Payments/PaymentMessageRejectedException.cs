namespace Storefront.Application.Payments;

/// <summary>
/// A message that claims to be from the provider was refused. The message text is for the journal and never contains
/// the body, the signature or anything about a buyer.
/// </summary>
public sealed class PaymentMessageRejectedException(string message) : Exception(message);
