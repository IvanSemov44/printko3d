using Microsoft.EntityFrameworkCore;
using Storefront.Application.Contracts;
using Storefront.Domain.Payments;
using Storefront.Infrastructure.Persistence;

namespace Storefront.Infrastructure.Repositories;

internal sealed class PaymentRepository(StorefrontDbContext context)
    : RepositoryBase<Payment>(context), IPaymentRepository
{
    public void CreateEvent(PaymentEvent paymentEvent) => Context.PaymentEvents.Add(paymentEvent);

    public Task<Payment?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken) =>
        Query(trackChanges: false)
            .Include(p => p.Order)
            .SingleOrDefaultAsync(p => p.PublicId == publicId, cancellationToken);

    public Task<Payment?> GetByReferenceAsync(string provider, string reference, CancellationToken cancellationToken) =>
        Query(trackChanges: true)
            .SingleOrDefaultAsync(p => p.Provider == provider && p.ProviderReference == reference, cancellationToken);

    public Task<Payment?> GetPendingForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        Query(trackChanges: false)
            .SingleOrDefaultAsync(p => p.OrderId == orderId && p.Status == PaymentStatus.Pending, cancellationToken);
}
