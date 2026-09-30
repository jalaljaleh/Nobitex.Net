using System.Collections.Concurrent;

namespace Nobitex.Net.Internal;

/// <summary>
/// Simple thread-safe token bucket used to stay below the documented Nobitex rate limits.
/// Each "bucket" is identified by an endpoint group (for example <c>orders</c> or <c>market-stats</c>).
/// </summary>
internal sealed class RateLimiter
{
    private sealed class Bucket
    {
        private readonly object _gate = new();
        private readonly int _capacity;
        private readonly TimeSpan _window;
        private readonly Queue<DateTimeOffset> _stamps = new();

        public Bucket(int capacity, TimeSpan window)
        {
            _capacity = capacity;
            _window = window;
        }

        /// <summary>Blocks the caller until a slot in the window is available.</summary>
        public async ValueTask AcquireAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                TimeSpan wait;

                lock (_gate)
                {
                    var now = DateTimeOffset.UtcNow;

                    while (_stamps.Count > 0 && now - _stamps.Peek() >= _window)
                    {
                        _stamps.Dequeue();
                    }

                    if (_stamps.Count < _capacity)
                    {
                        _stamps.Enqueue(now);
                        return;
                    }

                    wait = _window - (now - _stamps.Peek());
                }

                if (wait < TimeSpan.FromMilliseconds(10))
                {
                    wait = TimeSpan.FromMilliseconds(10);
                }

                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private readonly bool _enabled;

    /// <summary>Creates a rate limiter.</summary>
    /// <param name="enabled">When <c>false</c>, all calls are no-ops.</param>
    public RateLimiter(bool enabled) => _enabled = enabled;

    /// <summary>Waits until a request for the given group may be sent.</summary>
    public async ValueTask WaitAsync(string group, CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            return;
        }

        var bucket = _buckets.GetOrAdd(group, static (_, cfg) => new Bucket(cfg.capacity, cfg.window), DefaultGroup(group));

        await bucket.AcquireAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the documented limit for a known endpoint group.</summary>
    private static (int capacity, TimeSpan window) DefaultGroup(string group) => group switch
    {
        RateLimitGroups.PlaceOrder => (300, TimeSpan.FromMinutes(10)),   // shared spot + margin limit
        RateLimitGroups.OrderStatus => (300, TimeSpan.FromMinutes(1)),
        RateLimitGroups.OrderList => (30, TimeSpan.FromMinutes(1)),
        RateLimitGroups.CancelOrder => (90, TimeSpan.FromMinutes(1)),
        RateLimitGroups.MarketStats => (20, TimeSpan.FromMinutes(1)),
        RateLimitGroups.PublicTrades => (60, TimeSpan.FromMinutes(1)),
        RateLimitGroups.OrderBook => (300, TimeSpan.FromMinutes(1)),
        RateLimitGroups.Wallet => (60, TimeSpan.FromMinutes(1)),
        _ => (100, TimeSpan.FromMinutes(1)),
    };
}

/// <summary>Logical groups mapped to the rate limits published in the Nobitex documentation.</summary>
internal static class RateLimitGroups
{
    public const string PlaceOrder = "order-add";
    public const string OrderStatus = "order-status";
    public const string OrderList = "order-list";
    public const string CancelOrder = "order-cancel";
    public const string MarketStats = "market-stats";
    public const string PublicTrades = "public-trades";
    public const string OrderBook = "orderbook";
    public const string Wallet = "wallet";
    public const string Default = "default";
}