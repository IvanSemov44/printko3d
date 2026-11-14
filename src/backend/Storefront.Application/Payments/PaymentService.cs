using Storefront.Application.Contracts;
using Storefront.Application.Exceptions;
using Storefront.Application.Orders;
using Storefront.Domain.Orders;
using Storefront.Domain.Payments;

namespace Storefront.Application.Payments;

internal sealed class PaymentService(
    IRepositoryManager repositories,
    IPaymentProvider provider,
    IPaymentSimulator simulator,
    TimeProvider timeProvider,
    PaymentOptions options) : IPaymentService
{
    private const string EventRepeated = "payment.event_repeated";

    private const string AlreadyOpen = "payment.already_open";

    public async Task<PaymentSessionDto> StartAsync(int orderNumber, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var order = await repositories.Order.GetByNumberAsync(orderNumber, cancellationToken)
            ?? throw new NotFoundException($"Order {orderNumber} was not found.", "order.not_found");

        var now = timeProvider.GetUtcNow();
        if (order.Status != OrderStatus.New || order.PaymentDueAt <= now)
        {
            throw new ConflictException("The order cannot be paid any more.", "payment.order_not_payable");
        }

        // An order made before payment existed has no deadline: it gets a session of the usual length.
        var expiresAt = order.PaymentDueAt ?? now.AddMinutes(options.WindowMinutes);
        if (await repositories.Payments.GetPendingForOrderAsync(order.Id, cancellationToken) is { } waiting)
        {
            return ToSession(waiting, expiresAt);
        }

        var session = await provider.CreateSessionAsync(
            new PaymentSessionRequest(order.Number, order.TotalAmount, order.Currency, expiresAt),
            cancellationToken);
        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = provider.Code,
            ProviderReference = session.Reference,
            RedirectUrl = session.RedirectUrl,
            Amount = order.TotalAmount,
            Currency = order.Currency,
        };
        repositories.Payments.Create(payment);
        try
        {
            await repositories.SaveAsync(cancellationToken);
        }
        catch (ConflictException exception) when (exception.Code == AlreadyOpen)
        {
            // Two requests for the same order at the same moment: the one that lost takes the session that won.
            var winner = await repositories.Payments.GetPendingForOrderAsync(order.Id, cancellationToken);
            return winner is null ? throw new ConflictException(exception.Message, AlreadyOpen) : ToSession(winner, expiresAt);
        }

        return ToSession(payment, expiresAt);
    }

    public async Task<PaymentStatusDto> GetStatusAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await repositories.Payments.GetByPublicIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException("The payment was not found.", "payment.not_found");

        // A payment that never got an answer before the order was cancelled is told apart from one still waiting.
        var expired = payment.Status == PaymentStatus.Pending && payment.Order?.Status == OrderStatus.Cancelled;
        return new PaymentStatusDto(expired ? "Expired" : payment.Status.ToString());
    }

    public async Task<PaymentNotificationResult> HandleNotificationAsync(
        PaymentMessage message,
        CancellationToken cancellationToken)
    {
        var notification = await provider.ReadNotificationAsync(message, cancellationToken);
        var payment = await repositories.Payments.GetByReferenceAsync(provider.Code, notification.Reference, cancellationToken);
        if (payment is null)
        {
            return PaymentNotificationResult.UnknownPayment;
        }

        // The record of the message, the payment and the order are one transaction: a repeat or a failure leaves nothing.
        await using var transaction = await repositories.BeginTransactionAsync(cancellationToken);
        repositories.Payments.CreateEvent(new PaymentEvent
        {
            PaymentId = payment.Id,
            Provider = provider.Code,
            EventId = notification.EventId,
        });
        try
        {
            await repositories.SaveAsync(cancellationToken);
        }
        catch (ConflictException exception) when (exception.Code == EventRepeated)
        {
            return PaymentNotificationResult.Repeated;
        }

        var result = await ApplyAsync(payment, notification, cancellationToken);
        await repositories.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PaymentNotificationResult> CompleteTestPaymentAsync(
        string reference,
        PaymentOutcome outcome,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var payment = await repositories.Payments.GetByReferenceAsync(provider.Code, reference, cancellationToken)
            ?? throw new NotFoundException("The payment was not found.", "payment.not_found");

        // The sum is the one of the payment, never one the page sends.
        var message = await simulator.CreateMessageAsync(reference, outcome, payment.Amount, null, cancellationToken);
        return await HandleNotificationAsync(message, cancellationToken);
    }

    private async Task<PaymentNotificationResult> ApplyAsync(
        Payment payment,
        PaymentNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.Amount != payment.Amount)
        {
            return PaymentNotificationResult.AmountMismatch;
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return PaymentNotificationResult.Ignored;
        }

        payment.CompletedAt = timeProvider.GetUtcNow();
        if (notification.Outcome == PaymentOutcome.Failed)
        {
            payment.Status = PaymentStatus.Failed;
            return PaymentNotificationResult.Applied;
        }

        return await MarkPaidAsync(payment, cancellationToken);
    }

    private async Task<PaymentNotificationResult> MarkPaidAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (await repositories.Order.TryMarkPaidAsync(payment.OrderId, cancellationToken))
        {
            payment.Status = PaymentStatus.Succeeded;
            return PaymentNotificationResult.Applied;
        }

        // The order was not new: it is read again, because the expiry may have cancelled it a moment ago.
        var order = await repositories.Order.GetByIdAsync(payment.OrderId, trackChanges: false, cancellationToken)
            ?? throw new InvalidOperationException("The order of a payment is missing.");
        if (order.Status == OrderStatus.Paid)
        {
            payment.Status = PaymentStatus.Succeeded;
            return PaymentNotificationResult.Applied;
        }

        if (order.Status == OrderStatus.Cancelled && await TryReviveAsync(order, cancellationToken))
        {
            payment.Status = PaymentStatus.Succeeded;
            return PaymentNotificationResult.Revived;
        }

        payment.Status = PaymentStatus.PaidWithoutOrder;
        return PaymentNotificationResult.PaidWithoutOrder;
    }

    /// <summary>
    /// Takes the stock of a cancelled order again and makes it paid. All or nothing: when a line cannot be taken, or the
    /// order is not cancelled any more, what was taken goes back and the order stays as it is.
    /// </summary>
    private async Task<bool> TryReviveAsync(Order order, CancellationToken cancellationToken)
    {
        var taken = new List<OrderItem>();
        if (order.StockReleasedAt is not null)
        {
            foreach (var item in OrderStockLines.Of(order))
            {
                if (!await repositories.Product.TryTakeStockAsync(item.VariantId, item.Quantity, cancellationToken))
                {
                    await GiveBackAsync(taken, cancellationToken);
                    return false;
                }

                taken.Add(item);
            }
        }

        if (await repositories.Order.TryReviveAsync(order.Id, cancellationToken))
        {
            return true;
        }

        await GiveBackAsync(taken, cancellationToken);
        return false;
    }

    private async Task GiveBackAsync(IEnumerable<OrderItem> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            await repositories.Product.ReturnStockAsync(item.VariantId, item.Quantity, cancellationToken);
        }
    }

    private void EnsureEnabled()
    {
        if (!options.Enabled)
        {
            throw new NotFoundException("Payment is turned off.", "payment.disabled");
        }
    }

    private static PaymentSessionDto ToSession(Payment payment, DateTimeOffset expiresAt) =>
        new(payment.PublicId, payment.RedirectUrl, expiresAt);
}
