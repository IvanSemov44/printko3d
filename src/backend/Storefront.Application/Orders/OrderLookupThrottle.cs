using System.Collections.Concurrent;

namespace Storefront.Application.Orders;

/// <summary>
/// Counts the wrong email addresses tried for one order number (decision 0036). Order numbers are not secret and the
/// address of the client is not reliable behind the proxy (decisions 0014 and 0027), so the limit is on the number: after
/// a few misses the number is closed for a while, and the link with the token still works. It lives in memory, in one
/// process; a second server would need a shared store (task 33).
/// </summary>
public sealed class OrderLookupThrottle(TimeProvider timeProvider, OrderOptions options)
{
    private const int PruneAbove = 10_000;

    private readonly ConcurrentDictionary<int, Window> _windows = new();

    private sealed record Window(DateTimeOffset Start, int Failures);

    public bool IsBlocked(int number) =>
        _windows.TryGetValue(number, out var window)
        && !Expired(window)
        && window.Failures >= options.LookupFailuresPerOrder;

    public void RecordFailure(int number)
    {
        var now = timeProvider.GetUtcNow();
        _windows.AddOrUpdate(
            number,
            _ => new Window(now, 1),
            (_, current) => Expired(current) ? new Window(now, 1) : current with { Failures = current.Failures + 1 });

        // Numbers that nobody tries again would stay in memory for ever, so a big table is cleaned of the old ones.
        if (_windows.Count > PruneAbove)
        {
            foreach (var (key, window) in _windows.Where(pair => Expired(pair.Value)))
            {
                _windows.TryRemove(new KeyValuePair<int, Window>(key, window));
            }
        }
    }

    private bool Expired(Window window) =>
        timeProvider.GetUtcNow() - window.Start >= TimeSpan.FromMinutes(options.LookupFailureWindowMinutes);
}
