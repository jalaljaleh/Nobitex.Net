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

    public Task<MarketStats> GetStatsAsync(string? srcCurrency = null, string? dstCurrency = null, CancellationToken ct = default)
    {
        var url = "/market/stats";
        var q = new List<string>();
        if (!string.IsNullOrEmpty(srcCurrency)) q.Add($"srcCurrency={Uri.EscapeDataString(srcCurrency)}");
        if (!string.IsNullOrEmpty(dstCurrency)) q.Add($"dstCurrency={Uri.EscapeDataString(dstCurrency)}");
        if (q.Count > 0) url += "?" + string.Join("&", q);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), url));
        return _transport.SendAsync<MarketStats>(req, ct)!;
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
