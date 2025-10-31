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
        /// Get market statistics. Optional src/dst currency filters.
        /// Endpoint: GET /market/stats
        /// </summary>
        /// <param name="srcCurrency">Source currency code (optional).</param>
        /// <param name="dstCurrency">Destination currency code (optional).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="MarketStats"/>.</returns>
        Task<MarketStats> GetStatsAsync(string? srcCurrency = null, string? dstCurrency = null, CancellationToken ct = default);

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
