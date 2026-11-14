using System.Data.Common;
using Microsoft.Extensions.Options;
using Storefront.Application.Emails;

namespace Storefront.Api.Background;

/// <summary>
/// Sends the queued emails (decision 0036). A failed run is logged and tried again at the next interval; it never stops
/// the API. Only numbers are written to the log, never an address or a text.
/// </summary>
public sealed class EmailOutboxWorker(
    IServiceScopeFactory scopes,
    IOptions<EmailOutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.InitialDelaySeconds), timeProvider, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds), timeProvider);
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
            var outbox = scope.ServiceProvider.GetRequiredService<IEmailOutboxService>();
            var report = await outbox.SendDueAsync(cancellationToken);
            if (report.Sent > 0 || report.Retried > 0 || report.Failed > 0)
            {
                logger.LogInformation(
                    "Email outbox sent {SentEmails}, will retry {RetriedEmails} and gave up on {FailedEmails}",
                    report.Sent,
                    report.Retried,
                    report.Failed);
            }
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException
            && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Email outbox failed; it will run again at the next interval");
        }
    }
}
