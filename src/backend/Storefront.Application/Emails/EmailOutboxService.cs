using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Domain.Emails;

namespace Storefront.Application.Emails;

public sealed class EmailOutboxService(
    IRepositoryManager repositories,
    IEmailSender sender,
    TimeProvider timeProvider,
    EmailOutboxOptions options) : IEmailOutboxService
{
    private enum Attempt
    {
        Sent,
        Retried,
        Failed,
        Skipped,
    }

    public async Task<EmailOutboxReport> SendDueAsync(CancellationToken cancellationToken)
    {
        int sent = 0, retried = 0, failed = 0;
        var tried = new HashSet<Guid>();

        // A message that was tried is not tried again in this run, so one that keeps failing cannot hold the run in a loop.
        List<OutgoingEmail> batch;
        do
        {
            batch = (await repositories.OutgoingEmails.ListDueAsync(
                    timeProvider.GetUtcNow(), options.BatchSize, cancellationToken))
                .Where(email => tried.Add(email.Id))
                .ToList();
            foreach (var email in batch)
            {
                switch (await TrySendAsync(email, cancellationToken))
                {
                    case Attempt.Sent:
                        sent++;
                        break;
                    case Attempt.Retried:
                        retried++;
                        break;
                    case Attempt.Failed:
                        failed++;
                        break;
                    case Attempt.Skipped:
                        break;
                }
            }
        }
        while (batch.Count == options.BatchSize);

        var cutoff = timeProvider.GetUtcNow().AddDays(-options.RetentionDays);
        var deleted = await repositories.OutgoingEmails.DeleteSentBeforeAsync(cutoff, cancellationToken);
        return new EmailOutboxReport(sent, retried, failed, deleted);
    }

    private async Task<Attempt> TrySendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!await repositories.OutgoingEmails.TryClaimAsync(
                email.Id, now, now.AddSeconds(options.LeaseSeconds), cancellationToken))
        {
            return Attempt.Skipped;
        }

        // The claim counted this attempt in the database; the copy that was read still has the old count.
        var attempts = email.Attempts + 1;
        try
        {
            await sender.SendAsync(new EmailMessage(email.ToAddress, email.Subject, email.TextBody, email.HtmlBody), cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            if (attempts >= options.MaxAttempts)
            {
                await repositories.OutgoingEmails.MarkFailedAsync(email.Id, cancellationToken);
                return Attempt.Failed;
            }

            await repositories.OutgoingEmails.MarkRetryAsync(
                email.Id, timeProvider.GetUtcNow().Add(RetryPause(attempts)), cancellationToken);
            return Attempt.Retried;
        }

        await repositories.OutgoingEmails.MarkSentAsync(email.Id, cancellationToken);
        return Attempt.Sent;
    }

    // 60 s, 120 s, 240 s, ... after the first, second, third failed attempt.
    private TimeSpan RetryPause(int attempts) =>
        TimeSpan.FromSeconds(options.RetryBaseSeconds * Math.Pow(2, attempts - 1));
}
