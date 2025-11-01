using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class MarketClient : IMarketClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public MarketClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    /// <summary>
    /// Get recent trades for a market symbol.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /v2/trades/{symbol}
    /// Rate limit: 60 requests per minute
    /// No authentication required.
    /// Optional query filters: from (unix ms), to (unix ms), limit (max number of trades)
    /// </remarks>
    public Task<TradesHistoryResponse?> GetTradesAsync(string symbol, long? from = null, long? to = null, int? limit = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol is required.", nameof(symbol));
        var q = new List<string>();
        if (from.HasValue) q.Add($"from={Uri.EscapeDataString(from.Value.ToString())}");
        if (to.HasValue) q.Add($"to={Uri.EscapeDataString(to.Value.ToString())}");
        if (limit.HasValue) q.Add($"limit={Uri.EscapeDataString(limit.Value.ToString())}");
        var path = $"/v2/trades/{Uri.EscapeDataString(symbol)}" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<TradesHistoryResponse>(req, ct);
    }

    /// <summary>
    /// Get market statistics (optionally filtered by srcCurrency and/or dstCurrency).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /market/stats
    /// Rate limit: 20 requests per minute
    /// No authentication required.
    /// Query: srcCurrency, dstCurrency are optional. If omitted, stats for all markets are returned.
    /// </remarks>
    public Task<MarketStatsResponse?> GetStatsAsync(string? srcCurrency = null, string? dstCurrency = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(srcCurrency)) q.Add($"srcCurrency={Uri.EscapeDataString(srcCurrency)}");
        if (!string.IsNullOrWhiteSpace(dstCurrency)) q.Add($"dstCurrency={Uri.EscapeDataString(dstCurrency)}");
        var path = "/market/stats" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<MarketStatsResponse>(req, ct);
    }


    /// <summary>
    /// Get OHLC (UDF-compatible) history for a market.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /market/udf/history
    /// Rate limit: follow global market rate limits
    /// Query: symbol (required), resolution (required), to (required, unix seconds).
    /// Optional: from (unix seconds) or countback (overrides from), page (for paging).
    /// Response uses UDF format: s, t[], o[], h[], l[], c[], v[].
    /// </remarks>
    public Task<UdfHistoryResponse?> GetUdfHistoryAsync(
        string symbol,
        string resolution,
        long to,
        long? from = null,
        int? countback = null,
        int page = 1,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol is required.", nameof(symbol));
        if (string.IsNullOrWhiteSpace(resolution)) throw new ArgumentException("resolution is required.", nameof(resolution));
        if (to <= 0) throw new ArgumentException("to must be a unix timestamp (seconds).", nameof(to));
        if (page <= 0) page = 1;

        var q = new List<string>
            {
                $"symbol={Uri.EscapeDataString(symbol)}",
                $"resolution={Uri.EscapeDataString(resolution)}",
                $"to={Uri.EscapeDataString(to.ToString())}",
                $"page={Uri.EscapeDataString(page.ToString())}"
            };

        if (from.HasValue) q.Add($"from={Uri.EscapeDataString(from.Value.ToString())}");
        if (countback.HasValue) q.Add($"countback={Uri.EscapeDataString(countback.Value.ToString())}");

        var path = "/market/udf/history" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<UdfHistoryResponse>(req, ct);
    }

    /// <summary>
    /// Get orderbook for a single market symbol (or "all" for every market).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /v3/orderbook/{symbol} or /v3/orderbook/all
    /// Rate limit: 300 requests per minute
    /// No authentication required.
    /// </remarks>
    /// <param name="symbol">Market symbol (e.g., "BTCIRT") or "all".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized OrderbookResponse for a single symbol, or Dictionary for "all".</returns>
    public Task<OrderbookResponse?> GetOrderbookAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol is required.", nameof(symbol));
        var path = $"/v3/orderbook/{Uri.EscapeDataString(symbol)}";
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<OrderbookResponse>(req, ct);
    }

    /// <summary>
    /// Get orderbooks for all markets (calls GET /v3/orderbook/all).
    /// </summary>
    public Task<Dictionary<string, OrderbookSummary>?> GetAllOrderbooksAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/v3/orderbook/all"));
        return _transport.SendAsync<Dictionary<string, OrderbookSummary>>(req, ct);
    }


    /// <summary>
    /// Get supported margin markets and their settings.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /margin/markets/list
    /// Rate limit: 30 requests per minute
    /// Response contains a dictionary keyed by market symbol (e.g., "BTCIRT", "BTCUSDT").
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="MarginMarketsListResponse"/> or null.</returns>
    public Task<MarginMarketsListResponse?> GetMarginMarketsListAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/margin/markets/list"));
        return _transport.SendAsync<MarginMarketsListResponse>(req, ct);
    }


    /// <summary>
    /// Get active liquidity pools (capacity and filledCapacity per currency).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /liquidity-pools/list
    /// Rate limit: 12 requests per minute
    /// Response contains a dictionary keyed by currency symbol (e.g., "btc", "ltc", "doge").
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="LiquidityPoolsListResponse"/> or null.</returns>
    public Task<LiquidityPoolsListResponse?> GetLiquidityPoolsAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/liquidity-pools/list"));
        return _transport.SendAsync<LiquidityPoolsListResponse>(req, ct);
    }


    // reuse shared JsonSerializerOptions convention used in other methods (if needed elsewhere)
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
