using System.Text.Json;
using System.Text.Json.Nodes;
using Nobitex.Net.Internal;
using Nobitex.Net.Models;
using Nobitex.Net.Serialization;

namespace Nobitex.Net.Apis;

/// <summary>
/// Public market data endpoints. None of these calls require authentication.
/// </summary>
public sealed class MarketDataApi
{
    private readonly NobitexClient _client;

    internal MarketDataApi(NobitexClient client) => _client = client;

    /// <summary>
    /// Returns the latest statistics of one or many markets.
    /// Endpoint: <c>GET /market/stats</c> — rate limit: 20 requests / minute.
    /// </summary>
    /// <param name="srcCurrency">Comma separated source currencies, e.g. <c>"btc,usdt"</c>. Optional.</param>
    /// <param name="dstCurrency">Destination currency, e.g. <c>"rls"</c> or <c>"usdt"</c>. Optional.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics keyed as <c>src-dst</c> (for example <c>btc-rls</c>).</returns>
    public Task<MarketStatsResponse> GetMarketStatsAsync(
        string? srcCurrency = null,
        string? dstCurrency = null,
        CancellationToken cancellationToken = default)
    {
        var query = QueryBuilder.Build(new
        {
            srcCurrency,
            dstCurrency,
        });

        return _client.SendAsync<MarketStatsResponse>(
            HttpMethod.Get, "/market/stats", query, null, RateLimitGroups.MarketStats, authenticated: false, cancellationToken);
    }

    /// <summary>
    /// Returns the order book of a single market.
    /// Endpoint: <c>GET /v3/orderbook/{symbol}</c> — rate limit: 300 requests / minute.
    /// </summary>
    /// <param name="symbol">Market symbol, e.g. <c>BTCIRT</c>.</param>
    /// <param name="size">Maximum number of levels per side (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<OrderBook> GetOrderBookAsync(string symbol, int? size = null, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get,
            $"/v3/orderbook/{Uri.EscapeDataString(NobitexSymbol.Normalize(symbol))}",
            QueryBuilder.Build(new { size }),
            null,
            RateLimitGroups.OrderBook,
            authenticated: false,
            cancellationToken).ConfigureAwait(false);

        var book = node.Deserialize<OrderBook>(NobitexJson.Options)
            ?? throw new NobitexApiException("EmptyResponse", "Order book payload was empty.", 200);

        book.Symbol = NobitexSymbol.Normalize(symbol);
        return book;
    }

    /// <summary>
    /// Returns the order books of every active market in a single call.
    /// Endpoint: <c>GET /v3/orderbook/all</c>. Recommended when a bot tracks many markets.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, OrderBook>> GetAllOrderBooksAsync(CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/v3/orderbook/all", null, null,
            RateLimitGroups.OrderBook, authenticated: false, cancellationToken).ConfigureAwait(false);

        // The root mixes the "status" envelope with one entry per market, so it is removed first.
        if (node is JsonObject root)
        {
            root.Remove("status");
        }

        var books = node.Deserialize<Dictionary<string, OrderBook>>(NobitexJson.Options)
            ?? throw new NobitexApiException("EmptyResponse", "Order book payload was empty.", 200);

        foreach (var (symbol, book) in books)
        {
            book.Symbol = symbol;
        }

        return books;
    }

    /// <summary>
    /// Returns the aggregated depth of a market.
    /// Endpoint: <c>GET /v2/depth/{symbol}</c> (experimental).
    /// </summary>
    public Task<OrderBook> GetDepthAsync(string symbol, CancellationToken cancellationToken = default)
        => GetOrderBookAsync(symbol, null, cancellationToken);

    /// <summary>
    /// Returns the most recent public trades of a market.
    /// Endpoint: <c>GET /v2/trades/{symbol}</c> — rate limit: 60 requests / minute.
    /// </summary>
    /// <param name="symbol">Market symbol, e.g. <c>BTCIRT</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<PublicTrade>> GetPublicTradesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get,
            $"/v2/trades/{Uri.EscapeDataString(NobitexSymbol.Normalize(symbol))}",
            null, null, RateLimitGroups.PublicTrades, authenticated: false, cancellationToken).ConfigureAwait(false);

        var trades = node["trades"]?.Deserialize<List<PublicTrade>>(NobitexJson.Options) ?? new List<PublicTrade>();
        return trades;
    }

    /// <summary>
    /// Returns OHLC candles of a market (TradingView UDF format).
    /// Endpoint: <c>GET /market/udf/history</c> — maximum 500 candles per request.
    /// </summary>
    /// <param name="symbol">Market symbol.</param>
    /// <param name="resolution">Candle interval.</param>
    /// <param name="from">Start of the range (unix seconds). Optional.</param>
    /// <param name="to">End of the range (unix seconds). Required when <paramref name="countback"/> is not used.</param>
    /// <param name="countback">Number of candles before <paramref name="to"/>; has priority over <paramref name="from"/>.</param>
    /// <param name="page">Page number when the range contains more than 500 candles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CandleSeries> GetCandlesAsync(
        string symbol,
        CandleResolution resolution,
        long? from = null,
        long? to = null,
        int? countback = null,
        int? page = null,
        CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get,
            "/market/udf/history",
            QueryBuilder.Build(new
            {
                symbol = NobitexSymbol.Normalize(symbol),
                resolution = NobitexSymbol.ToResolution(resolution),
                from,
                to,
                countback,
                page,
            }),
            null,
            RateLimitGroups.MarketStats,
            authenticated: false,
            cancellationToken).ConfigureAwait(false);

        return new CandleSeries
        {
            Symbol = NobitexSymbol.Normalize(symbol),
            Resolution = resolution,
            Candles = CandleSeries.ParseUdf(node),
        };
    }

    /// <summary>
    /// Returns <c>true</c> when the market is currently open for trading.
    /// </summary>
    public async Task<bool> IsMarketOpenAsync(string srcCurrency, string dstCurrency, CancellationToken cancellationToken = default)
    {
        var stats = await GetMarketStatsAsync(srcCurrency, dstCurrency, cancellationToken).ConfigureAwait(false);
        return stats.Stats.TryGetValue($"{srcCurrency.ToLowerInvariant()}-{dstCurrency.ToLowerInvariant()}", out var s) && !s.IsClosed;
    }
}