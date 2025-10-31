using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class TradesClient : ITradesClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public TradesClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    public Task<IReadOnlyList<Trade>> GetTradesAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentNullException(nameof(symbol));
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), $"/v2/trade/history/{Uri.EscapeDataString(symbol)}"));
        return _transport.SendAsync<IReadOnlyList<Trade>>(req, ct)!;
    }

    /// <summary>
    /// Place a new market order.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /market/orders/add
    /// Rate limit: 300 requests per 10 minutes (shared)
    /// Body:
    /// - type (string) required: "buy" or "sell"
    /// - execution (string) optional: "limit" (default) or "market"
    /// - srcCurrency (string) required: source currency code (e.g., "btc")
    /// - dstCurrency (string) required: destination currency code (e.g., "rls")
    /// - amount (string) required: quantity as monetary string (use invariant culture)
    /// - price (string or number) required for limit orders: unit price
    /// - clientOrderId (string) optional: up to 32 chars, unique per user among open orders
    /// </remarks>
    /// <param name="request">Order payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="OrderAddResponse"/> or null.</returns>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    public Task<OrderAddResponse?> AddOrderAsync(AddOrderRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/market/orders/add"))
        {
            Content = content
        };

        return _transport.SendAsync<OrderAddResponse>(req, ct);
    }
    /// <summary>
    /// Place a stop order (stop_market or stop_limit).
    /// </summary>
    /// <param name="type">"buy" or "sell".</param>
    /// <param name="srcCurrency">Source currency code (e.g., "btc").</param>
    /// <param name="dstCurrency">Destination currency code (e.g., "rls").</param>
    /// <param name="amount">Amount (monetary string, use invariant culture).</param>
    /// <param name="execution">"stop_market" or "stop_limit".</param>
    /// <param name="stopPrice">Stop price (monetary).</param>
    /// <param name="price">For stop_limit, the limit price; for stop_market leave null.</param>
    /// <param name="clientOrderId">Optional client order id (<=32 chars).</param>
    public Task<OrderAddResponse?> PlaceStopOrderAsync(
        string type,
        string srcCurrency,
        string dstCurrency,
        string amount,
        string execution,
        decimal stopPrice,
        decimal? price = null,
        string? clientOrderId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        if (string.IsNullOrWhiteSpace(srcCurrency)) throw new ArgumentNullException(nameof(srcCurrency));
        if (string.IsNullOrWhiteSpace(dstCurrency)) throw new ArgumentNullException(nameof(dstCurrency));
        if (string.IsNullOrWhiteSpace(amount)) throw new ArgumentNullException(nameof(amount));
        if (execution != "stop_market" && execution != "stop_limit") throw new ArgumentException("execution must be 'stop_market' or 'stop_limit'", nameof(execution));

        var req = new AddOrderRequest(
            Type: type,
            Execution: execution,
            SrcCurrency: srcCurrency,
            DstCurrency: dstCurrency,
            Amount: amount,
            Price: price.HasValue ? price.Value.ToString(CultureInfo.InvariantCulture) : null,
            ClientOrderId: clientOrderId,
            // stop-specific fields
            StopPrice: stopPrice.ToString(CultureInfo.InvariantCulture),
            StopLimitPrice: null,
            Mode: null
        );

        return AddOrderAsync(req, ct);
    }

    /// <summary>
    /// Place an OCO order (one-cancels-the-other). This will submit the OCO mode as documented.
    /// </summary>
    /// <param name="type">"buy" or "sell".</param>
    /// <param name="srcCurrency">Source currency code.</param>
    /// <param name="dstCurrency">Destination currency code.</param>
    /// <param name="amount">Amount as monetary string.</param>
    /// <param name="price">Primary order price (monetary).</param>
    /// <param name="stopPrice">Stop (trigger) price.</param>
    /// <param name="stopLimitPrice">Stop-limit price used for the stop-limit leg.</param>
    /// <param name="clientOrderId">Optional client order id.</param>
    public Task<OrderAddResponse?> PlaceOcoOrderAsync(
        string type,
        string srcCurrency,
        string dstCurrency,
        string amount,
        decimal price,
        decimal stopPrice,
        decimal stopLimitPrice,
        string? clientOrderId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        if (string.IsNullOrWhiteSpace(srcCurrency)) throw new ArgumentNullException(nameof(srcCurrency));
        if (string.IsNullOrWhiteSpace(dstCurrency)) throw new ArgumentNullException(nameof(dstCurrency));
        if (string.IsNullOrWhiteSpace(amount)) throw new ArgumentNullException(nameof(amount));

        var req = new AddOrderRequest(
            Type: type,
            Execution: "limit", // primary leg is a limit; the mode "oco" indicates the pair
            SrcCurrency: srcCurrency,
            DstCurrency: dstCurrency,
            Amount: amount,
            Price: price.ToString(CultureInfo.InvariantCulture),
            ClientOrderId: clientOrderId,
            StopPrice: stopPrice.ToString(CultureInfo.InvariantCulture),
            StopLimitPrice: stopLimitPrice.ToString(CultureInfo.InvariantCulture),
            Mode: "oco"
        );

        return AddOrderAsync(req, ct);
    }

    /// <summary>
    /// Get status of a single order by id or clientOrderId.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /market/orders/status
    /// Rate limit: 300 requests per minute
    /// At least one of <paramref name="request.Id"/> or <paramref name="request.ClientOrderId"/> must be provided.
    /// If both are provided, the server gives priority to Id.
    /// </remarks>
    /// <param name="request">Request containing either Id or ClientOrderId (or both).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="OrderStatusResponse"/> or null.</returns>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    /// <exception cref="ArgumentException">If both Id and ClientOrderId are null/empty.</exception>
    public Task<OrderStatusResponse?> GetOrderStatusAsync(OrderStatusRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (!request.Id.HasValue && string.IsNullOrWhiteSpace(request.ClientOrderId))
            throw new ArgumentException("Either Id or ClientOrderId must be provided.", nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/market/orders/status"))
        {
            Content = content
        };

        return _transport.SendAsync<OrderStatusResponse>(req, ct);
    }


    /// <summary>
    /// Get the user's orders list with optional filters and paging.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /market/orders/list
    /// Rate limit: 30 requests per minute
    /// Query parameters (all optional unless noted):
    /// - status: all | open | done | close (default open)
    /// - type: buy | sell
    /// - execution: limit | market | stop_limit | stop_market
    /// - tradeType: spot | margin
    /// - srcCurrency: source currency code (e.g., btc)
    /// - dstCurrency: destination currency code (e.g., usdt)
    /// - details: 1 | 2 (1 = less fields, 2 = more fields like id, status, fee, created_at)
    /// - fromId: int (returns orders with id > fromId when provided)
    /// - page: page number (mutually exclusive with fromId)
    /// - pageSize: number of items per page (default 100, max 1000)
    /// - order: ordering key (id, -id, created_at, -created_at, price, -price)
    /// </remarks>
    public Task<OrdersListResponse?> GetUserOrdersAsync(
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
        CancellationToken ct = default)
    {
        var q = new List<string>();

        if (!string.IsNullOrWhiteSpace(status)) q.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(type)) q.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(execution)) q.Add($"execution={Uri.EscapeDataString(execution)}");
        if (!string.IsNullOrWhiteSpace(tradeType)) q.Add($"tradeType={Uri.EscapeDataString(tradeType)}");
        if (!string.IsNullOrWhiteSpace(srcCurrency)) q.Add($"srcCurrency={Uri.EscapeDataString(srcCurrency)}");
        if (!string.IsNullOrWhiteSpace(dstCurrency)) q.Add($"dstCurrency={Uri.EscapeDataString(dstCurrency)}");
        if (details.HasValue) q.Add($"details={Uri.EscapeDataString(details.Value.ToString())}");
        if (fromId.HasValue)
        {
            q.Add($"fromId={Uri.EscapeDataString(fromId.Value.ToString())}");
        }
        else
        {
            if (page.HasValue) q.Add($"page={Uri.EscapeDataString(page.Value.ToString())}");
            if (pageSize.HasValue) q.Add($"pageSize={Uri.EscapeDataString(pageSize.Value.ToString())}");
        }
        if (!string.IsNullOrWhiteSpace(order)) q.Add($"order={Uri.EscapeDataString(order)}");

        var path = "/market/orders/list" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<OrdersListResponse>(req, ct);
    }

    /// <summary>
    /// Update order status (cancel or change activation).
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /market/orders/update-status
    /// Rate limit: 90 requests per minute
    /// At least one of <paramref name="request.Order"/> or <paramref name="request.ClientOrderId"/> must be provided.
    /// If both are provided, the server gives priority to Order.
    /// Allowed status transitions (server-side): new -> active, active/inactive -> canceled.
    /// </remarks>
    /// <param name="request">Request containing order id or clientOrderId and target status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="UpdateOrderStatusResponse"/> or null.</returns>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    /// <exception cref="ArgumentException">If neither Order nor ClientOrderId is provided or Status is null/empty.</exception>
    public Task<UpdateOrderStatusResponse?> UpdateOrderStatusAsync(UpdateOrderStatusRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (!request.Order.HasValue && string.IsNullOrWhiteSpace(request.ClientOrderId))
            throw new ArgumentException("Either Order or ClientOrderId must be provided.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Status))
            throw new ArgumentException("Status must be provided.", nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/market/orders/update-status"))
        {
            Content = content
        };

        return _transport.SendAsync<UpdateOrderStatusResponse>(req, ct);
    }



    /// <summary>
    /// Cancel active orders in bulk that match filters and are older than given hours.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /market/orders/cancel-old
    /// Rate limit: 30 requests per minute
    /// Body parameters (all optional):
    /// - hours (float): orders older than this many hours will be cancelled. If omitted, all matching active orders are cancelled.
    /// - execution (string): filter by execution type (market | limit | stop_market | stop_limit).
    /// - tradeType (string): filter by trade type (spot | margin).
    /// - srcCurrency (string): source currency code (e.g., btc).
    /// - dstCurrency (string): destination currency code (e.g., rls).
    /// Note: stop orders that are inactive and non-OCO are not affected by this endpoint.
    /// </remarks>
    /// <param name="request">CancelOldRequest containing optional filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="CancelOldResponse"/> or null.</returns>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    public Task<CancelOldResponse?> CancelOldAsync(CancelOldRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/market/orders/cancel-old"))
        {
            Content = content
        };

        return _transport.SendAsync<CancelOldResponse>(req, ct);
    }


    /// <summary>
    /// Get the user's trades (last 3 days, paginated).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /market/trades/list
    /// Rate limit: 30 requests per minute
    /// Default page size: 30
    /// Filters: if srcCurrency is provided then dstCurrency must also be provided (and vice versa).
    /// fromId: optional minimum trade id (inclusive).
    /// </remarks>
    /// <param name="srcCurrency">Source currency code (e.g., "usdt") — must be provided together with dstCurrency or both null.</param>
    /// <param name="dstCurrency">Destination currency code (e.g., "rls") — must be provided together with srcCurrency or both null.</param>
    /// <param name="fromId">Optional minimum trade id (inclusive) to page from.</param>
    /// <param name="page">Page number (1-based). Default 1.</param>
    /// <param name="pageSize">Items per page. Default 30.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="TradesListResponse"/> or null.</returns>
    public Task<TradesListResponse?> GetUserTradesAsync(
        string? srcCurrency = null,
        string? dstCurrency = null,
        long? fromId = null,
        int page = 1,
        int pageSize = 30,
        CancellationToken ct = default)
    {
        // validate paired market filter
        if ((string.IsNullOrWhiteSpace(srcCurrency) && !string.IsNullOrWhiteSpace(dstCurrency)) ||
            (!string.IsNullOrWhiteSpace(srcCurrency) && string.IsNullOrWhiteSpace(dstCurrency)))
        {
            throw new ArgumentException("srcCurrency and dstCurrency must be both provided or both null/empty.");
        }

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 30;

        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(srcCurrency)) q.Add($"srcCurrency={Uri.EscapeDataString(srcCurrency)}");
        if (!string.IsNullOrWhiteSpace(dstCurrency)) q.Add($"dstCurrency={Uri.EscapeDataString(dstCurrency)}");
        if (fromId.HasValue) q.Add($"fromId={Uri.EscapeDataString(fromId.Value.ToString())}");
        else
        {
            q.Add($"page={Uri.EscapeDataString(page.ToString())}");
            q.Add($"pageSize={Uri.EscapeDataString(pageSize.ToString())}");
        }

        var path = "/market/trades/list" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<TradesListResponse>(req, ct);
    }


    /// <summary>
    /// Place a margin order (single or OCO).
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /margin/orders/add
    /// Rate limit: 300 requests per 10 minutes
    /// Supports normal margin orders and OCO when Mode == "oco".
    /// Required: srcCurrency, dstCurrency, amount, price (for limit), leverage (defaults to 1).
    /// For OCO: provide Mode = "oco" and both stopPrice and stopLimitPrice.
    /// Monetary fields are strings to preserve precision; helpers accept decimals for convenience.
    /// </remarks>
    /// <param name="request">Margin order payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="MarginOrderResponse"/>.</returns>
    public Task<MarginOrderResponse?> AddMarginOrderAsync(MarginOrderRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.SrcCurrency)) throw new ArgumentException("srcCurrency is required.", nameof(request.SrcCurrency));
        if (string.IsNullOrWhiteSpace(request.DstCurrency)) throw new ArgumentException("dstCurrency is required.", nameof(request.DstCurrency));
        if (string.IsNullOrWhiteSpace(request.Amount)) throw new ArgumentException("amount is required.", nameof(request.Amount));
        if (string.IsNullOrWhiteSpace(request.Price) && string.IsNullOrWhiteSpace(request.Execution))
        {
            // execution defaults to limit server-side but price is required for limit
        }
        // If mode is oco ensure stopPrice and stopLimitPrice present
        if (string.Equals(request.Mode, "oco", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.StopPrice) || string.IsNullOrWhiteSpace(request.StopLimitPrice))
                throw new ArgumentException("stopPrice and stopLimitPrice are required for OCO (Mode = 'oco').");
        }

        // Accept numeric leverage as string, but validate range minimally (>=1)
        if (!string.IsNullOrWhiteSpace(request.Leverage))
        {
            if (!decimal.TryParse(request.Leverage, NumberStyles.Number, CultureInfo.InvariantCulture, out var lev) || lev < 1m)
                throw new ArgumentException("leverage must be a decimal >= 1.", nameof(request.Leverage));
        }

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/margin/orders/add"))
        {
            Content = content
        };

        return _transport.SendAsync<MarginOrderResponse>(req, ct);
    }


    /// <summary>
    /// Get the user's positions (open and historical).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /positions/list
    /// Rate limit: 30 requests per minute
    /// Paging: default pageSize = 50
    /// Query:
    /// - srcCurrency, dstCurrency: optional filters (can be provided independently)
    /// - status: "active" (default) or "past"
    /// - page, pageSize: paging controls (pageSize max should be respected by caller; default 50)
    /// - fromId: optional (alternative paging)
    /// </remarks>
    public Task<PositionsListResponse?> GetPositionsListAsync(
        string? srcCurrency = null,
        string? dstCurrency = null,
        string status = "active",
        long? fromId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        // validate status
        if (string.IsNullOrWhiteSpace(status)) status = "active";
        var statusLower = status.Trim().ToLowerInvariant();
        if (statusLower != "active" && statusLower != "past")
            throw new ArgumentException("status must be 'active' or 'past'.", nameof(status));

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 50;

        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(srcCurrency)) q.Add($"srcCurrency={Uri.EscapeDataString(srcCurrency!)}");
        if (!string.IsNullOrWhiteSpace(dstCurrency)) q.Add($"dstCurrency={Uri.EscapeDataString(dstCurrency!)}");
        q.Add($"status={Uri.EscapeDataString(statusLower)}");

        if (fromId.HasValue)
        {
            q.Add($"fromId={Uri.EscapeDataString(fromId.Value.ToString())}");
        }
        else
        {
            q.Add($"page={Uri.EscapeDataString(page.ToString())}");
            q.Add($"pageSize={Uri.EscapeDataString(pageSize.ToString())}");
        }

        var path = "/positions/list" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<PositionsListResponse>(req, ct);
    }

    /// <summary>
    /// Get status for a single position by id.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /positions/{positionId}/status
    /// Rate limit: 100 requests per 10 minutes (shared)
    /// </remarks>
    /// <param name="positionId">Position identifier (required).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="PositionStatusResponse"/> or null.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If positionId is not positive.</exception>
    public Task<PositionStatusResponse?> GetPositionStatusAsync(long positionId, CancellationToken ct = default)
    {
        if (positionId <= 0) throw new ArgumentOutOfRangeException(nameof(positionId), "positionId must be positive.");

        var path = $"/positions/{positionId}/status";
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<PositionStatusResponse>(req, ct);
    }

    /// <summary>
    /// Close a position by placing a margin order in the opposite side (single or OCO).
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /positions/{positionId}/close
    /// Rate limit: 300 requests per 10 minutes
    /// - For a normal close provide amount and price (or execution=market); for OCO set Mode="oco" and include stopPrice/stopLimitPrice.
    /// - Monetary fields are strings to preserve precision; use invariant culture when parsing.
    /// - The API returns a single Order (or Orders[] when Mode == "oco").
    /// </remarks>
    /// <param name="positionId">Position identifier (required, positive).</param>
    /// <param name="request">Close position request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="ClosePositionResponse"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If positionId is not positive.</exception>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    /// <exception cref="ArgumentException">If required fields are missing or invalid (e.g., OCO missing fields).</exception>
    public Task<ClosePositionResponse?> ClosePositionAsync(long positionId, ClosePositionRequest request, CancellationToken ct = default)
    {
        if (positionId <= 0) throw new ArgumentOutOfRangeException(nameof(positionId), "positionId must be positive.");
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Basic validation
        if (string.Equals(request.Mode, "oco", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Price)) throw new ArgumentException("price is required for OCO.", nameof(request.Price));
            if (string.IsNullOrWhiteSpace(request.StopPrice)) throw new ArgumentException("stopPrice is required for OCO.", nameof(request.StopPrice));
            if (string.IsNullOrWhiteSpace(request.StopLimitPrice)) throw new ArgumentException("stopLimitPrice is required for OCO.", nameof(request.StopLimitPrice));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Amount)) throw new ArgumentException("amount is required.", nameof(request.Amount));
            // price is required for limit execution; if execution omitted server defaults to limit so require price unless execution=market
            if (!string.Equals(request.Execution, "market", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.Price))
                throw new ArgumentException("price is required for limit/stop_limit executions.", nameof(request.Price));
        }

        // minimal numeric validation for amount (preserve as string for request)
        if (!string.IsNullOrWhiteSpace(request.Amount))
        {
            if (!decimal.TryParse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0m)
                throw new ArgumentException("amount must be a positive decimal string.", nameof(request.Amount));
        }

        var path = $"/positions/{positionId}/close";
        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), path)) { Content = content };

        return _transport.SendAsync<ClosePositionResponse>(req, ct);
    }



    /// <summary>
    /// Edit collateral of an open position (increase or decrease).
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /positions/{positionId}/edit-collateral
    /// Rate limit: 60 requests per minute
    /// Body:
    /// - collateral (string) required: new collateral amount (monetary string, preserve precision)
    /// </remarks>
    /// <param name="positionId">Position identifier (required, positive).</param>
    /// <param name="request">Edit collateral payload (required).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="EditCollateralResponse"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If positionId is not positive.</exception>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    /// <exception cref="ArgumentException">If collateral is null/empty or not a positive decimal string.</exception>
    public Task<EditCollateralResponse?> EditPositionCollateralAsync(long positionId, EditCollateralRequest request, CancellationToken ct = default)
    {
        if (positionId <= 0) throw new ArgumentOutOfRangeException(nameof(positionId), "positionId must be positive.");
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Collateral)) throw new ArgumentException("collateral is required.", nameof(request.Collateral));

        // basic numeric validation while keeping string representation for API
        if (!decimal.TryParse(request.Collateral, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var coll) || coll < 0m)
            throw new ArgumentException("collateral must be a non-negative decimal string.", nameof(request.Collateral));

        var path = $"/positions/{positionId}/edit-collateral";
        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), path)) { Content = content };

        return _transport.SendAsync<EditCollateralResponse>(req, ct);
    }

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
