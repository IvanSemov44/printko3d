namespace Storefront.Application.Contracts;

/// <summary>A plain email: one recipient, a subject, a text body and optionally an HTML body.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null)
{
    /// <summary>Names the message in the log. The recipient and the body are never written there.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}

/// <summary>Sends email (decision 0031). Where it goes is a setting: a local catcher in development, a provider in production.</summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the message. When no mail server is configured the message is skipped and a warning is written to the log, so
    /// a demo without keys keeps working. Throws <see cref="Exceptions.EmailDeliveryException"/> when a configured server
    /// does not take the message.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
