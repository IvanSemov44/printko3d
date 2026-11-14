using System.Data.Common;
using Microsoft.Extensions.Options;
using Storefront.Application.Payments;

namespace Storefront.Api.Background;

/// <summary>
/// Cancels the unpaid orders after their deadline and gives their stock back (decision 0035). A failed run is logged and
/// tried again at the next interval; it never stops the API. Only the number of orders is written, never anything about a buyer.
/// </summary>
public sealed class PaymentExpiryWorker(
    IServiceScopeFactory scopes,
    IOptions<PaymentOptions> options,
    TimeProvider timeProvider,
    ILogger<PaymentExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.ExpiryInitialDelaySeconds), timeProvider, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.ExpiryIntervalMinutes), timeProvider);
            do
            {
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The API is stopping.
        }
    }

    /// <summary>One run in its own scope. Only database failures are swallowed; a bug in the code still stops the run loudly.</summary>
    public async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var expiry = scope.ServiceProvider.GetRequiredService<IPaymentExpiryService>();
            var report = await expiry.CancelExpiredAsync(cancellationToken);
            if (report.Cancelled > 0 || report.Failed > 0)
            {
                logger.LogInformation(
                    "Payment expiry cancelled {CancelledOrders} orders; {FailedOrders} failed and will be tried again",
                    report.Cancelled,
                    report.Failed);
            }
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException
            && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Payment expiry failed; it will run again at the next interval");
        }
    }
}
