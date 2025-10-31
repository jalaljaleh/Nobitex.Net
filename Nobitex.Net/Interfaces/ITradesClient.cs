using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net
{
    /// <summary>
    /// Trade and orders related endpoints for the Nobitex API.
    /// Implementations must be safe for concurrent calls.
    /// </summary>
    public interface ITradesClient
    {
        /// <summary>
        /// Get public trade history for a market symbol.
        /// Endpoint: GET /v2/trade/history/{symbol}
        /// </summary>
        /// <param name="symbol">Market symbol (required).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of recent trades.</returns>
        Task<IReadOnlyList<Trade>> GetTradesAsync(string symbol, CancellationToken ct = default);

        /// <summary>
        /// Place a new order (spot).
        /// Endpoint: POST /market/orders/add
        /// </summary>
        /// <param name="request">Order payload.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Order add response or null.</returns>
        Task<OrderAddResponse?> AddOrderAsync(AddOrderRequest request, CancellationToken ct = default);

        /// <summary>
        /// Place a stop order (stop_market or stop_limit).
        /// </summary>
        /// <param name="type">"buy" or "sell".</param>
        /// <param name="srcCurrency">Source currency code.</param>
        /// <param name="dstCurrency">Destination currency code.</param>
        /// <param name="amount">Amount as monetary string.</param>
        /// <param name="execution">"stop_market" or "stop_limit".</param>
        /// <param name="stopPrice">Stop trigger price.</param>
        /// <param name="price">Limit price for stop_limit; null for stop_market.</param>
        /// <param name="clientOrderId">Optional client order id (<=32 chars).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Order add response or null.</returns>
        Task<OrderAddResponse?> PlaceStopOrderAsync(
            string type,
            string srcCurrency,
            string dstCurrency,
            string amount,
            string execution,
            decimal stopPrice,
            decimal? price = null,
            string? clientOrderId = null,
            CancellationToken ct = default);

        /// <summary>
        /// Place an OCO order (one-cancels-the-other).
        /// </summary>
        /// <param name="type">"buy" or "sell".</param>
        /// <param name="srcCurrency">Source currency code.</param>
        /// <param name="dstCurrency">Destination currency code.</param>
        /// <param name="amount">Amount as monetary string.</param>
        /// <param name="price">Primary order price.</param>
        /// <param name="stopPrice">Stop trigger price.</param>
        /// <param name="stopLimitPrice">Stop-limit price for the stop leg.</param>
        /// <param name="clientOrderId">Optional client order id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Order add response or null.</returns>
        Task<OrderAddResponse?> PlaceOcoOrderAsync(
            string type,
            string srcCurrency,
            string dstCurrency,
            string amount,
            decimal price,
            decimal stopPrice,
            decimal stopLimitPrice,
            string? clientOrderId = null,
            CancellationToken ct = default);

        /// <summary>
        /// Get status of a single order by id or clientOrderId.
        /// Endpoint: POST /market/orders/status
        /// </summary>
        /// <param name="request">Order status request (Id or ClientOrderId required).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Order status response or null.</returns>
        Task<OrderStatusResponse?> GetOrderStatusAsync(OrderStatusRequest request, CancellationToken ct = default);

        /// <summary>
        /// Get the user's orders list with optional filters and paging.
        /// Endpoint: GET /market/orders/list
        /// </summary>
        Task<OrdersListResponse?> GetUserOrdersAsync(
            string? status = null,
            string? type = null,
            string? execution = null,
            string? tradeType = null,
            string? srcCurrency = null,
            string? dstCurrency = null,
            int? details = null,
            long? fromId = null,
            int? page = null,
            int? pageSize = null,
            string? order = null,
            CancellationToken ct = default);

        /// <summary>
        /// Update order status (cancel or change activation).
        /// Endpoint: POST /market/orders/update-status
        /// </summary>
        /// <param name="request">Update request containing Order or ClientOrderId and target Status.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Update response or null.</returns>
        Task<UpdateOrderStatusResponse?> UpdateOrderStatusAsync(UpdateOrderStatusRequest request, CancellationToken ct = default);

        /// <summary>
        /// Cancel active orders in bulk that match filters and are older than given hours.
        /// Endpoint: POST /market/orders/cancel-old
        /// </summary>
        /// <param name="request">Cancel old request containing optional filters.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Cancel old response or null.</returns>
        Task<CancelOldResponse?> CancelOldAsync(CancelOldRequest request, CancellationToken ct = default);

        /// <summary>
        /// Get the user's trades (last 3 days, paginated).
        /// Endpoint: GET /market/trades/list
        /// </summary>
        Task<TradesListResponse?> GetUserTradesAsync(
            string? srcCurrency = null,
            string? dstCurrency = null,
            long? fromId = null,
            int page = 1,
            int pageSize = 30,
            CancellationToken ct = default);

        /// <summary>
        /// Place a margin order (single or OCO).
        /// Endpoint: POST /margin/orders/add
        /// </summary>
        /// <param name="request">Margin order payload.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Margin order response or null.</returns>
        Task<MarginOrderResponse?> AddMarginOrderAsync(MarginOrderRequest request, CancellationToken ct = default);

        /// <summary>
        /// Get the user's positions (open and historical).
        /// Endpoint: GET /positions/list
        /// </summary>
        Task<PositionsListResponse?> GetPositionsListAsync(
            string? srcCurrency = null,
            string? dstCurrency = null,
            string status = "active",
            long? fromId = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default);

        /// <summary>
        /// Get status for a single position by id.
        /// Endpoint: GET /positions/{positionId}/status
        /// </summary>
        Task<PositionStatusResponse?> GetPositionStatusAsync(long positionId, CancellationToken ct = default);

        /// <summary>
        /// Close a position by placing a margin order in the opposite side.
        /// Endpoint: POST /positions/{positionId}/close
        /// </summary>
        Task<ClosePositionResponse?> ClosePositionAsync(long positionId, ClosePositionRequest request, CancellationToken ct = default);

        /// <summary>
        /// Edit collateral of an open position (increase or decrease).
        /// Endpoint: POST /positions/{positionId}/edit-collateral
        /// </summary>
        Task<EditCollateralResponse?> EditPositionCollateralAsync(long positionId, EditCollateralRequest request, CancellationToken ct = default);
    }
}
