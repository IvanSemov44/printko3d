using Storefront.Domain.Emails;

namespace Storefront.Application.Contracts;

/// <summary>The queue of outgoing emails (decision 0036).</summary>
public interface IOutgoingEmailRepository
{
    /// <summary>Marks the message as new, due now, for the next save. The caller saves it in the transaction of its own change.</summary>
    void Enqueue(EmailMessage message);

    /// <summary>The pending messages whose time has come, the oldest first. At most <paramref name="batchSize"/>. Not tracked.</summary>
    Task<IReadOnlyList<OutgoingEmail>> ListDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// <c>WHERE status = Pending AND next_attempt_at &lt;= now</c>: counts the attempt and holds the message until
    /// <paramref name="leaseUntil"/>, in one statement, so two workers never send the same message. Returns false when
    /// someone else took it.
    /// </summary>
    Task<bool> TryClaimAsync(Guid id, DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken cancellationToken);

    Task MarkSentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The attempt failed: the message waits until <paramref name="nextAttemptAt"/>.</summary>
    Task MarkRetryAsync(Guid id, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);

    /// <summary>The last attempt failed: the message is kept as failed and never sent again.</summary>
    Task MarkFailedAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes messages that were sent before <paramref name="cutoff"/> (they hold personal data). Returns how many.</summary>
    Task<int> DeleteSentBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
