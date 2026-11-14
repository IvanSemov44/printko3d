namespace Storefront.Application.Emails;

/// <summary>What one run of the sender did. Only numbers: never an address or a text.</summary>
public sealed record EmailOutboxReport(int Sent, int Retried, int Failed, int Deleted);

public interface IEmailOutboxService
{
    /// <summary>
    /// Sends the messages whose time has come through <c>IEmailSender</c>. A message the server does not take waits and is
    /// tried again after a longer pause; after the last attempt it is kept as failed. Old sent messages are deleted.
    /// </summary>
    Task<EmailOutboxReport> SendDueAsync(CancellationToken cancellationToken);
}
