namespace Storefront.Domain.Emails;

/// <summary>
/// An email that waits to be sent (decision 0036). It is written in the same transaction as the change that caused it, so
/// a status never changes without its message, and a mail server that is down does not lose the message. The address
/// and the text are personal data: they are never written to the log.
/// </summary>
public sealed class OutgoingEmail : Entity
{
    public string ToAddress { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string TextBody { get; set; } = string.Empty;

    public string? HtmlBody { get; set; }

    public OutgoingEmailStatus Status { get; set; } = OutgoingEmailStatus.Pending;

    /// <summary>How many times sending was started. Counted when the message is taken, so a crash in the middle still counts.</summary>
    public int Attempts { get; set; }

    /// <summary>The earliest moment for the next attempt. While a worker holds the message, it is the end of its lease.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
