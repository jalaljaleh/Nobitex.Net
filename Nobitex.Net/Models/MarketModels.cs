using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Nobitex.Net.Models;

/// <summary>Response of <c>GET /market/stats</c>.</summary>
public sealed class MarketStatsResponse
{
    /// <summary>Statistics keyed as <c>src-dst</c>, for example <c>btc-rls</c>.</summary>
    [JsonPropertyName("stats")]
    public Dictionary<string, MarketStats> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Helper indexer by symbol, e.g. <c>response["btc-rls"]</c>.</summary>
    public MarketStats? this[string key] => Stats.TryGetValue(key, out var value) ? value : null;
}

/// <summary>Statistics of a single market.</summary>
public sealed class MarketStats
{
    /// <summary>Is the market currently closed?</summary>
    public bool IsClosed { get; set; }

    /// <summary>Best ask (lowest sell) price.</summary>
    public decimal BestSell { get; set; }

    /// <summary>Best bid (highest buy) price.</summary>
    public decimal BestBuy { get; set; }

    /// <summary>24h traded volume expressed in the source currency.</summary>
    public decimal VolumeSrc { get; set; }

    /// <summary>24h traded volume expressed in the destination currency.</summary>
    public decimal VolumeDst { get; set; }

    /// <summary>Latest traded price.</summary>
    public decimal Latest { get; set; }

    /// <summary>Mark price.</summary>
    public decimal Mark { get; set; }

    /// <summary>Lowest price of the last 24 hours.</summary>
    public decimal DayLow { get; set; }

    /// <summary>Highest price of the last 24 hours.</summary>
    public decimal DayHigh { get; set; }

    /// <summary>Price 24 hours ago (open).</summary>
    public decimal DayOpen { get; set; }

    /// <summary>Current close price.</summary>
    public decimal DayClose { get; set; }

    /// <summary>24 hour change in percent.</summary>
    public decimal DayChange { get; set; }

    /// <summary>Mid price between best bid and best ask.</summary>
    public decimal Mid => (BestBuy + BestSell) / 2m;

    /// <summary>Relative spread in percent.</summary>
    public decimal SpreadPercent => BestBuy == 0 ? 0 : (BestSell - BestBuy) / BestBuy * 100m;
}

/// <summary>A single price level of an order book.</summary>
public sealed record OrderBookLevel(decimal Price, decimal Amount)
{
    /// <summary>Notional value of the level (price * amount).</summary>
    public decimal Total => Price * Amount;
}

/// <summary>Order book of a market (response of <c>GET /v3/orderbook/{symbol}</c>).</summary>
public sealed class OrderBook
{
    /// <summary>Market symbol, e.g. <c>BTCIRT</c>. Filled by the client.</summary>
    public string? Symbol { get; set; }

    /// <summary>Timestamp of the last update (unix milliseconds).</summary>
    [JsonPropertyName("lastUpdate")]
    public DateTimeOffset LastUpdate { get; set; }

    /// <summary>Price of the last trade.</summary>
    [JsonPropertyName("lastTradePrice")]
    public decimal LastTradePrice { get; set; }

    /// <summary>Ask (sell) levels, sorted ascending by price.</summary>
    public List<OrderBookLevel> Asks { get; set; } = new();

    /// <summary>Bid (buy) levels, sorted descending by price.</summary>
    public List<OrderBookLevel> Bids { get; set; } = new();

    /// <summary>Best available bid (null when the book is empty).</summary>
    public OrderBookLevel? BestBid => Bids.Count == 0 ? null : Bids[0];

    /// <summary>Best available ask (null when the book is empty).</summary>
    public OrderBookLevel? BestAsk => Asks.Count == 0 ? null : Asks[0];

    /// <summary>Mid price of the book.</summary>
    public decimal Mid => BestBid is null || BestAsk is null ? LastTradePrice : (BestBid.Price + BestAsk.Price) / 2m;

    /// <summary>Sum of all bid volumes within the given percentage of the best bid.</summary>
    public decimal BidDepth(double percent = 1) => Depth(Bids, percent);

    /// <summary>Sum of all ask volumes within the given percentage of the best ask.</summary>
    public decimal AskDepth(double percent = 1) => Depth(Asks, percent);

    private static decimal Depth(List<OrderBookLevel> levels, double percent)
    {
        if (levels.Count == 0)
        {
            return 0m;
        }

        var reference = levels[0].Price;
        var threshold = reference * (decimal)(1 + percent / 100d);
        decimal sum = 0;

        foreach (var level in levels)
        {
            if (Math.Abs(level.Price - reference) > threshold)
            {
                break;
            }

            sum += level.Amount;
        }

        return sum;
    }
}

/// <summary>A public trade from <c>GET /v2/trades/{symbol}</c>.</summary>
public sealed class PublicTrade
{
    /// <summary>Trade time (unix milliseconds).</summary>
    public DateTimeOffset Time { get; set; }

    /// <summary>Traded price.</summary>
    public decimal Price { get; set; }

    /// <summary>Traded volume in the source currency.</summary>
    public decimal Volume { get; set; }

    /// <summary>Side of the taker.</summary>
    public TradeSide Type { get; set; }
}

/// <summary>OHLC candle series returned by <c>GET /market/udf/history</c>.</summary>
public sealed class CandleSeries
{
    /// <summary>Market symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Resolution of the candles.</summary>
    public CandleResolution Resolution { get; set; }

    /// <summary>Parsed candles.</summary>
    public IReadOnlyList<Candle> Candles { get; set; } = Array.Empty<Candle>();

    /// <summary>Converts the TradingView UDF arrays into <see cref="Candle"/> instances.</summary>
    internal static IReadOnlyList<Candle> ParseUdf(JsonNode node)
    {
        double[] Times = ReadArray(node["t"]);
        double[] Open = ReadArray(node["o"]);
        double[] High = ReadArray(node["h"]);
        double[] Low = ReadArray(node["l"]);
        double[] Close = ReadArray(node["c"]);
        double[] Volume = ReadArray(node["v"]);

        var list = new List<Candle>(Times.Length);
        for (var i = 0; i < Times.Length; i++)
        {
            list.Add(new Candle
            {
                OpenTime = DateTimeOffset.FromUnixTimeSeconds((long)Times[i]),
                Open = (decimal)Open[i],
                High = (decimal)High[i],
                Low = (decimal)Low[i],
                Close = (decimal)Close[i],
                Volume = (decimal)Volume[i],
            });
        }

        return list;
    }

    private static double[] ReadArray(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return Array.Empty<double>();
        }

        var result = new double[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            result[i] = array[i]?.GetValue<double>() ?? 0d;
        }

        return result;
    }
}

/// <summary>A single OHLCV candle.</summary>
public sealed class Candle
{
    /// <summary>Opening time of the candle.</summary>
    public DateTimeOffset OpenTime { get; set; }

    /// <summary>Open price.</summary>
    public decimal Open { get; set; }

    /// <summary>Highest price.</summary>
    public decimal High { get; set; }

    /// <summary>Lowest price.</summary>
    public decimal Low { get; set; }

    /// <summary>Close price.</summary>
    public decimal Close { get; set; }

    /// <summary>Traded volume.</summary>
    public decimal Volume { get; set; }
}