using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Emails;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class OutgoingEmailRepository(StorefrontDbContext context)
    : RepositoryBase<OutgoingEmail>(context), IOutgoingEmailRepository
{
    public void Enqueue(EmailMessage message) =>
        Create(new OutgoingEmail
        {
            ToAddress = message.To,
            Subject = message.Subject,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
            NextAttemptAt = Context.Now,
        });

    public async Task<IReadOnlyList<OutgoingEmail>> ListDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken) =>
        await Query(trackChanges: false)
            .Where(e => e.Status == OutgoingEmailStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(
        Guid id,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken)
    {
        var stamp = Context.Now;
        var changed = await Context.OutgoingEmails
            .Where(e => e.Id == id && e.Status == OutgoingEmailStatus.Pending && e.NextAttemptAt <= now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.NextAttemptAt, leaseUntil)
                    .SetProperty(e => e.Attempts, e => e.Attempts + 1)
                    .SetProperty(e => e.UpdatedAt, stamp),
                cancellationToken);
        return changed == 1;
    }

    public async Task MarkSentAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = Context.Now;
        await Context.OutgoingEmails
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.Status, OutgoingEmailStatus.Sent)
                    .SetProperty(e => e.SentAt, now)
                    .SetProperty(e => e.UpdatedAt, now),
                cancellationToken);
    }

    public async Task MarkRetryAsync(Guid id, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
    {
        var now = Context.Now;
        await Context.OutgoingEmails
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.NextAttemptAt, nextAttemptAt).SetProperty(e => e.UpdatedAt, now),
                cancellationToken);
    }

    public async Task MarkFailedAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = Context.Now;
        await Context.OutgoingEmails
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.Status, OutgoingEmailStatus.Failed).SetProperty(e => e.UpdatedAt, now),
                cancellationToken);
    }

    public Task<int> DeleteSentBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        Context.OutgoingEmails
            .Where(e => e.Status == OutgoingEmailStatus.Sent && e.SentAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
