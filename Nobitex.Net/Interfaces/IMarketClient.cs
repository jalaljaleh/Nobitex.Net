using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net
{
    /// <summary>
    /// Market-related endpoints for the Nobitex API.
    /// Implementations should be safe for concurrent use.
    /// </summary>
    public interface IMarketClient
    {

        /// <summary>
        /// Get recent trades for a market symbol.
        /// Optional filters: from (unix ms), to (unix ms), limit (count).
        /// </summary>
        Task<TradesHistoryResponse?> GetTradesAsync(string symbol, long? from = null, long? to = null, int? limit = null, CancellationToken ct = default);
        
        
        /// <summary>
        /// Get market statistics optionally filtered by srcCurrency and/or dstCurrency.
        /// </summary>
        Task<MarketStatsResponse?> GetStatsAsync(string? srcCurrency = null, string? dstCurrency = null, CancellationToken ct = default);


        /// <summary>
        /// Get OHLC history (UDF) for a market symbol.
        /// resolution examples: 1,5,15,30,60,180,240,360,720,D,2D,3D
        /// from/to are unix seconds. Use countback to request N bars before 'to'.
        /// </summary>
        Task<UdfHistoryResponse?> GetUdfHistoryAsync(string symbol, string resolution, long to, long? from = null, int? countback = null, int page = 1, CancellationToken ct = default);


        /// <summary>
        /// Get orderbook for a single market symbol (or "all" for every market).
        /// Returns a single-symbol OrderbookResponse when symbol != "all".
        /// </summary>
        /// <param name="symbol">Market symbol (e.g., "BTCIRT") or "all".</param>
        /// <param name="ct">Cancellation token.</param>
        Task<OrderbookResponse?> GetOrderbookAsync(string symbol, CancellationToken ct = default);

        /// <summary>
        /// Get orderbooks for all markets (GET /v3/orderbook/all).
        /// Returns a dictionary mapping symbol -> OrderbookSummary.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task<Dictionary<string, OrderbookSummary>?> GetAllOrderbooksAsync(CancellationToken ct = default);

        /// <summary>
        /// Get supported margin markets and their settings.
        /// Endpoint: GET /margin/markets/list
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="MarginMarketsListResponse"/> or null.</returns>
        Task<MarginMarketsListResponse?> GetMarginMarketsListAsync(CancellationToken ct = default);

        /// <summary>
        /// Get active liquidity pools (capacity and filledCapacity per currency).
        /// Endpoint: GET /liquidity-pools/list
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="LiquidityPoolsListResponse"/> or null.</returns>
        Task<LiquidityPoolsListResponse?> GetLiquidityPoolsAsync(CancellationToken ct = default);
    }
}
