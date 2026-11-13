namespace Storefront.Application.Exceptions;

/// <summary>
/// A configured mail server did not take the message. The text names the kind of failure only: never the server, the
/// user, the password or the recipient.
/// </summary>
public sealed class EmailDeliveryException(string message) : Exception(message);
