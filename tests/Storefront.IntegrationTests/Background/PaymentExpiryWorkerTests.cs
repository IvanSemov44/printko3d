using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storefront.Api.Background;
using Storefront.Application.Payments;

namespace Storefront.IntegrationTests.Background;

public sealed class PaymentExpiryWorkerTests
{
    private sealed class FakeExpiry(Func<CancellationToken, Task<PaymentExpiryReport>> run) : IPaymentExpiryService
    {
        public int Calls { get; private set; }

        public Task<PaymentExpiryReport> CancelExpiredAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return run(cancellationToken);
        }
    }

    private sealed class CapturingLogger : ILogger<PaymentExpiryWorker>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FailingDbException : DbException;

    private static (PaymentExpiryWorker Worker, FakeExpiry Expiry, CapturingLogger Logger) Create(
        Func<CancellationToken, Task<PaymentExpiryReport>> run)
    {
        var expiry = new FakeExpiry(run);
        var services = new ServiceCollection().AddScoped<IPaymentExpiryService>(_ => expiry).BuildServiceProvider();
        var logger = new CapturingLogger();
        var worker = new PaymentExpiryWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PaymentOptions()),
            TimeProvider.System,
            logger);
        return (worker, expiry, logger);
    }

    [Fact]
    public async Task ARun_CallsTheExpiryOnce_AndLogsHowManyOrdersWereCancelled()
    {
        var (worker, expiry, logger) = Create(_ => Task.FromResult(new PaymentExpiryReport(3, 1)));

        await worker.RunCycleAsync(default);

        Assert.Equal(1, expiry.Calls);
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Information
                && e.Message.Contains("3 orders", StringComparison.Ordinal)
                && e.Message.Contains("1 failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARunThatCancelsNothing_WritesNothing()
    {
        var (worker, _, logger) = Create(_ => Task.FromResult(new PaymentExpiryReport(0, 0)));

        await worker.RunCycleAsync(default);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ADatabaseFailure_IsLogged_AndDoesNotStopTheWorker()
    {
        var (worker, _, logger) = Create(_ => throw new FailingDbException());

        await worker.RunCycleAsync(default);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ABugInTheCode_IsNotHidden()
    {
        var (worker, _, logger) = Create(_ => throw new ArgumentException("a bug"));

        await Assert.ThrowsAsync<ArgumentException>(() => worker.RunCycleAsync(default));

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AStopRequest_IsNotLoggedAsAFailure()
    {
        using var source = new CancellationTokenSource();
        var (worker, _, logger) = Create(token =>
        {
            source.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new PaymentExpiryReport(0, 0));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunCycleAsync(source.Token));

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }
}
