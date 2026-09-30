using System.Collections.Concurrent;
using Nobitex.Net.Models;

namespace Nobitex.Net.WebSocket;

/// <summary>
/// Maintains an in-memory order book that is kept up to date by the WebSocket publications.
/// The implementation is thread safe: readers always see a consistent snapshot.
/// </summary>
public sealed class LocalOrderBook
{
    private readonly ConcurrentDictionary<decimal, decimal> _bids = new();
    private readonly ConcurrentDictionary<decimal, decimal> _asks = new();

    /// <summary>Creates a local order book for a symbol.</summary>
    public LocalOrderBook(string symbol) => Symbol = symbol;

    /// <summary>Market symbol.</summary>
    public string Symbol { get; }

    /// <summary>Timestamp of the last applied update.</summary>
    public DateTimeOffset LastUpdate { get; private set; }

    /// <summary>Reads a consistent snapshot of the current book.</summary>
    public OrderBook Current => Build();

    /// <summary>Replaces the local book with a full snapshot.</summary>
    public void Apply(OrderBook snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_bids)
        {
            _bids.Clear();
            _asks.Clear();

            foreach (var level in snapshot.Bids)
            {
                _bids[level.Price] = level.Amount;
            }

            foreach (var level in snapshot.Asks)
            {
                _asks[level.Price] = level.Amount;
            }

            LastUpdate = snapshot.LastUpdate;
        }
    }

    /// <summary>Applies a delta update: a zero amount removes the price level.</summary>
    public void ApplyDelta(bool isBid, decimal price, decimal amount)
    {
        var book = isBid ? _bids : _asks;

        if (amount <= 0)
        {
            book.TryRemove(price, out _);
        }
        else
        {
            book[price] = amount;
        }
    }

    /// <summary>Best bid price, or <c>null</c> when the book is empty.</summary>
    public decimal? BestBid => MaxKey(_bids);

    /// <summary>Best ask price, or <c>null</c> when the book is empty.</summary>
    public decimal? BestAsk => MinKey(_asks);

    /// <summary>Top N bid levels sorted by price descending.</summary>
    public IReadOnlyList<OrderBookLevel> TopBids(int count = 10) => Top(_bids, count, descending: true);

    /// <summary>Top N ask levels sorted by price ascending.</summary>
    public IReadOnlyList<OrderBookLevel> TopAsks(int count = 10) => Top(_asks, count, descending: false);

    private OrderBook Build()
    {
        lock (_bids)
        {
            return new OrderBook
            {
                Symbol = Symbol,
                LastUpdate = LastUpdate,
                Bids = Top(_bids, _bids.Count, descending: true).ToList(),
                Asks = Top(_asks, _asks.Count, descending: false).ToList(),
            };
        }
    }

    private static IReadOnlyList<OrderBookLevel> Top(
        ConcurrentDictionary<decimal, decimal> source, int count, bool descending)
    {
        var query = descending ? source.OrderByDescending(kv => kv.Key) : source.OrderBy(kv => kv.Key);

        return query.Take(Math.Max(0, count))
            .Select(kv => new OrderBookLevel(kv.Key, kv.Value))
            .ToList();
    }

    private static decimal? MaxKey(ConcurrentDictionary<decimal, decimal> source)
    {
        decimal? best = null;

        foreach (var key in source.Keys)
        {
            if (best is null || key > best)
            {
                best = key;
            }
        }

        return best;
    }

    private static decimal? MinKey(ConcurrentDictionary<decimal, decimal> source)
    {
        decimal? best = null;

        foreach (var key in source.Keys)
        {
            if (best is null || key < best)
            {
                best = key;
            }
        }

        return best;
    }
}