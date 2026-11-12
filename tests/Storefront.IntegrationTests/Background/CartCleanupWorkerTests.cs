using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storefront.Api.Background;
using Storefront.Api.Setup;
using Storefront.Application.Carts;

namespace Storefront.IntegrationTests.Background;

public sealed class CartCleanupWorkerTests
{
    private sealed class FakeCleanup(Func<CancellationToken, Task<int>> run) : ICartCleanupService
    {
        public int Calls { get; private set; }

        public Task<int> DeleteStaleAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return run(cancellationToken);
        }
    }

    private sealed class CapturingLogger : ILogger<CartCleanupWorker>
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

    private static (CartCleanupWorker Worker, FakeCleanup Cleanup, CapturingLogger Logger) Create(
        Func<CancellationToken, Task<int>> run)
    {
        var cleanup = new FakeCleanup(run);
        var services = new ServiceCollection().AddScoped<ICartCleanupService>(_ => cleanup).BuildServiceProvider();
        var logger = new CapturingLogger();
        var worker = new CartCleanupWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CartCleanupOptions()),
            TimeProvider.System,
            logger);
        return (worker, cleanup, logger);
    }

    [Fact]
    public async Task ARun_CallsTheCleanupOnce_AndLogsHowManyCartsWereDeleted()
    {
        var (worker, cleanup, logger) = Create(_ => Task.FromResult(3));

        await worker.RunCycleAsync(default);

        Assert.Equal(1, cleanup.Calls);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("3 carts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADatabaseFailure_IsLogged_AndDoesNotStopTheWorker()
    {
        var (worker, _, logger) = Create(_ => throw new FailingDbException());

        await worker.RunCycleAsync(default);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AnInvalidOperation_IsLogged_AndDoesNotStopTheWorker()
    {
        var (worker, _, logger) = Create(_ => throw new InvalidOperationException("transient failure"));

        await worker.RunCycleAsync(default);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ATimeout_IsLogged_AndDoesNotStopTheWorker()
    {
        var (worker, _, logger) = Create(_ => throw new TimeoutException());

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
            return Task.FromResult(0);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunCycleAsync(source.Token));

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }
}
