namespace Storefront.Domain.Emails;

public enum OutgoingEmailStatus
{
    /// <summary>Waits to be sent, or to be sent again after a failed attempt.</summary>
    Pending,

    /// <summary>The mail server took the message.</summary>
    Sent,

    /// <summary>Every attempt failed. The message is kept for a person to look at; nothing sends it again.</summary>
    Failed,
}
