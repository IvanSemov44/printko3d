using System.Data.Common;
using Microsoft.Extensions.Options;
using Storefront.Api.Setup;
using Storefront.Application.Carts;

namespace Storefront.Api.Background;

/// <summary>Runs the cart cleanup once a day. A failed run is logged and tried again at the next interval; it never stops the API.</summary>
public sealed class CartCleanupWorker(
    IServiceScopeFactory scopes,
    IOptions<CartCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<CartCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.InitialDelaySeconds), timeProvider, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(settings.IntervalHours), timeProvider);
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
            var cleanup = scope.ServiceProvider.GetRequiredService<ICartCleanupService>();
            var deleted = await cleanup.DeleteStaleAsync(cancellationToken);
            logger.LogInformation("Cart cleanup deleted {DeletedCarts} carts", deleted);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException
            && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Cart cleanup failed; it will run again at the next interval");
        }
    }
}
