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

    /// <summary>
    /// Place a new market/limit order.
    /// Endpoint: POST /market/orders/add
    /// </summary>
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
    /// Convenience helper that builds AddOrderRequest and calls AddOrderAsync.
    /// </summary>
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
            StopPrice: stopPrice.ToString(CultureInfo.InvariantCulture),
            StopLimitPrice: null,
            Mode: null
        );

        return AddOrderAsync(req, ct);
    }

    /// <summary>
    /// Place an OCO order (one-cancels-the-other).
    /// Convenience helper that builds AddOrderRequest and calls AddOrderAsync.
    /// </summary>
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
            Execution: "limit",
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

    // ----- Margin orders -----

    /// <summary>
    /// Place a margin order (single or OCO).
    /// Endpoint: POST /margin/orders/add
    /// </summary>
    public Task<MarginOrderResponse?> AddMarginOrderAsync(MarginOrderRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.SrcCurrency)) throw new ArgumentException("srcCurrency is required.", nameof(request.SrcCurrency));
        if (string.IsNullOrWhiteSpace(request.DstCurrency)) throw new ArgumentException("dstCurrency is required.", nameof(request.DstCurrency));
        if (string.IsNullOrWhiteSpace(request.Amount)) throw new ArgumentException("amount is required.", nameof(request.Amount));

        if (string.Equals(request.Mode, "oco", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.StopPrice) || string.IsNullOrWhiteSpace(request.StopLimitPrice))
                throw new ArgumentException("stopPrice and stopLimitPrice are required for OCO (Mode = 'oco').");
        }

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

    // ----- Orders status / list / update / bulk cancel -----

    /// <summary>
    /// Get status of a single order by id or clientOrderId.
    /// Endpoint: POST /market/orders/status
    /// </summary>
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
    /// Endpoint: GET /market/orders/list
    /// </summary>
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
    /// Endpoint: POST /market/orders/update-status
    /// </summary>
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
    /// Endpoint: POST /market/orders/cancel-old
    /// </summary>
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

    // ----- User trades list -----

    /// <summary>
    /// Get the user's trades (last 3 days, paginated).
    /// Endpoint: GET /market/trades/list
    /// </summary>
    public Task<TradesListResponse?> GetUserTradesAsync(
        string? srcCurrency = null,
        string? dstCurrency = null,
        long? fromId = null,
        int page = 1,
        int pageSize = 30,
        CancellationToken ct = default)
    {
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

    // ----- Positions (get list / status / close / edit collateral) -----

    /// <summary>
    /// Get the user's positions (open and historical).
    /// Endpoint: GET /positions/list
    /// </summary>
    public Task<PositionsListResponse?> GetPositionsListAsync(
        string? srcCurrency = null,
        string? dstCurrency = null,
        string status = "active",
        long? fromId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
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
    /// Endpoint: GET /positions/{positionId}/status
    /// </summary>
    public Task<PositionStatusResponse?> GetPositionStatusAsync(long positionId, CancellationToken ct = default)
    {
        if (positionId <= 0) throw new ArgumentOutOfRangeException(nameof(positionId), "positionId must be positive.");

        var path = $"/positions/{positionId}/status";
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<PositionStatusResponse>(req, ct);
    }

    /// <summary>
    /// Close a position by placing a margin order in the opposite side (single or OCO).
    /// Endpoint: POST /positions/{positionId}/close
    /// </summary>
    public Task<ClosePositionResponse?> ClosePositionAsync(long positionId, ClosePositionRequest request, CancellationToken ct = default)
    {
        if (positionId <= 0) throw new ArgumentOutOfRangeException(nameof(positionId), "positionId must be positive.");
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (string.Equals(request.Mode, "oco", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Price)) throw new ArgumentException("price is required for OCO.", nameof(request.Price));
            if (string.IsNullOrWhiteSpace(request.StopPrice)) throw new ArgumentException("stopPrice is required for OCO.", nameof(request.StopPrice));
            if (string.IsNullOrWhiteSpace(request.StopLimitPrice)) throw new ArgumentException("stopLimitPrice is required for OCO.", nameof(request.StopLimitPrice));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Amount)) throw new ArgumentException("amount is required.", nameof(request.Amount));
            if (!string.Equals(request.Execution, "market", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.Price))
                throw new ArgumentException("price is required for limit/stop_limit executions.", nameof(request.Price));
        }

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
    /// Endpoint: POST /positions/{positionId}/edit-collateral
    /// </summary>
    public Task<EditCollateralResponse?> EditPositionCollateralAsync(long positionId, EditCollateralRequest request, CancellationToken ct = default)
    {
        if (positionId <= 0) throw new ArgumentOutOfRangeException(nameof(positionId), "positionId must be positive.");
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Collateral)) throw new ArgumentException("collateral is required.", nameof(request.Collateral));

        if (!decimal.TryParse(request.Collateral, NumberStyles.Number, CultureInfo.InvariantCulture, out var coll) || coll < 0m)
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
