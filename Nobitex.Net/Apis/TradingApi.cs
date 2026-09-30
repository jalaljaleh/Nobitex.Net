using System.Text.Json;
using System.Text.Json.Nodes;
using Nobitex.Net.Internal;
using Nobitex.Net.Models;
using Nobitex.Net.Serialization;

namespace Nobitex.Net.Apis;

/// <summary>
/// Spot trading endpoints. Requires the <c>TRADE</c> permission for write operations and
/// <c>READ</c> for queries. All order placing endpoints share the limit of
/// <b>300 requests / 10 minutes</b>.
/// </summary>
public sealed class TradingApi
{
    private readonly NobitexClient _client;

    internal TradingApi(NobitexClient client) => _client = client;

    /// <summary>
    /// Places a new order on the spot market.
    /// Endpoint: <c>POST /market/orders/add</c> — 300 requests / 10 minutes (shared limit).
    /// </summary>
    /// <param name="request">Order definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created order. Note that creation does not imply execution.</returns>
    /// <exception cref="NobitexOrderRejectedException">Thrown when the matching engine rejects the order.</exception>
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var payload = new Dictionary<string, object?>
        {
            ["type"] = NobitexSymbol.ToApiString(request.Side),
            ["execution"] = NobitexSymbol.ToApiString(request.Execution),
            ["srcCurrency"] = request.SrcCurrency,
            ["dstCurrency"] = request.DstCurrency,
            ["amount"] = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["price"] = request.Execution is OrderExecution.Market or OrderExecution.StopMarket && !request.Price.HasValue
                ? null
                : request.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stopPrice"] = request.StopPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stopLimitPrice"] = request.StopLimitPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mode"] = request.Mode,
            ["clientOrderId"] = request.ClientOrderId,
            ["pro"] = request.ProMode ? "yes" : null,
        };

        var node = await _client.SendNodeAsync(
            HttpMethod.Post, "/market/orders/add", null, payload,
            RateLimitGroups.PlaceOrder, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["order"]?.Deserialize<Order>(NobitexJson.Options)
            ?? throw new NobitexApiException("EmptyResponse", "The order payload was empty.", 200);
    }

    /// <summary>Places a limit order (executes only at the given price or better).</summary>
    public Task<Order> PlaceLimitOrderAsync(
        OrderSide side, string srcCurrency, string dstCurrency, decimal amount, decimal price,
        string? clientOrderId = null, CancellationToken cancellationToken = default)
        => PlaceOrderAsync(new PlaceOrderRequest
        {
            Side = side,
            Execution = OrderExecution.Limit,
            SrcCurrency = srcCurrency,
            DstCurrency = dstCurrency,
            Amount = amount,
            Price = price,
            ClientOrderId = clientOrderId,
        }, cancellationToken);

    /// <summary>
    /// Places a market order. Supplying <paramref name="expectedPrice"/> is strongly recommended;
    /// it acts as a protective price band so the order cannot be filled at an unexpected price.
    /// </summary>
    public Task<Order> PlaceMarketOrderAsync(
        OrderSide side, string srcCurrency, string dstCurrency, decimal amount,
        decimal? expectedPrice = null, string? clientOrderId = null, CancellationToken cancellationToken = default)
        => PlaceOrderAsync(new PlaceOrderRequest
        {
            Side = side,
            Execution = OrderExecution.Market,
            SrcCurrency = srcCurrency,
            DstCurrency = dstCurrency,
            Amount = amount,
            Price = expectedPrice,
            ClientOrderId = clientOrderId,
        }, cancellationToken);

    /// <summary>
    /// Places a stop-loss / stop-entry order. The order stays <see cref="OrderStatus.Inactive"/>
    /// until the market reaches <paramref name="stopPrice"/>.
    /// </summary>
    /// <param name="side"></param>
    /// <param name="execution"><see cref="OrderExecution.StopMarket"/> or <see cref="OrderExecution.StopLimit"/>.</param>
    /// <param name="srcCurrency"></param>
    /// <param name="dstCurrency"></param>
    /// <param name="amount"></param>
    /// <param name="stopPrice"></param>
    /// <param name="price"></param>
    /// <param name="clientOrderId"></param>
    /// <param name="cancellationToken"></param>
    public Task<Order> PlaceStopOrderAsync(
        OrderSide side, OrderExecution execution, string srcCurrency, string dstCurrency,
        decimal amount, decimal stopPrice, decimal? price = null, string? clientOrderId = null,
        CancellationToken cancellationToken = default)
        => PlaceOrderAsync(new PlaceOrderRequest
        {
            Side = side,
            Execution = execution,
            SrcCurrency = srcCurrency,
            DstCurrency = dstCurrency,
            Amount = amount,
            Price = price,
            StopPrice = stopPrice,
            ClientOrderId = clientOrderId,
        }, cancellationToken);

    /// <summary>
    /// Places an OCO order (one limit order + one stop-limit order). When one side executes the
    /// other side is cancelled automatically by Nobitex.
    /// </summary>
    /// <returns>The two linked orders.</returns>
    public async Task<IReadOnlyList<Order>> PlaceOcoOrderAsync(
        OrderSide side, string srcCurrency, string dstCurrency, decimal amount,
        decimal price, decimal stopPrice, decimal stopLimitPrice,
        string? clientOrderId = null, CancellationToken cancellationToken = default)
    {
        var request = new PlaceOrderRequest
        {
            Side = side,
            Execution = OrderExecution.Limit,
            SrcCurrency = srcCurrency,
            DstCurrency = dstCurrency,
            Amount = amount,
            Price = price,
            StopPrice = stopPrice,
            StopLimitPrice = stopLimitPrice,
            Mode = "oco",
            ClientOrderId = clientOrderId,
        };
        request.Validate();

        var node = await _client.SendNodeAsync(
            HttpMethod.Post, "/market/orders/add", null, BuildPlacePayload(request),
            RateLimitGroups.PlaceOrder, authenticated: true, cancellationToken).ConfigureAwait(false);

        var orders = node["orders"]?.Deserialize<List<Order>>(NobitexJson.Options) ?? new List<Order>();
        return orders;
    }

    /// <summary>
    /// Places several orders in one call.
    /// Endpoint: <c>POST /market/orders/batch-add</c>.
    /// </summary>
    public async Task<IReadOnlyList<Order>> PlaceBatchOrdersAsync(
        IEnumerable<PlaceOrderRequest> orders, CancellationToken cancellationToken = default)
    {
        var list = orders?.ToList() ?? throw new ArgumentNullException(nameof(orders));
        if (list.Count == 0)
        {
            return Array.Empty<Order>();
        }

        foreach (var order in list)
        {
            order.Validate();
        }

        var payload = new Dictionary<string, object?>
        {
            ["orders"] = list.Select(BuildPlacePayload).ToList(),
        };

        var node = await _client.SendNodeAsync(
            HttpMethod.Post, "/market/orders/batch-add", null, payload,
            RateLimitGroups.PlaceOrder, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["orders"]?.Deserialize<List<Order>>(NobitexJson.Options) ?? new List<Order>();
    }

    /// <summary>
    /// Returns the current status of a single order.
    /// Endpoint: <c>POST /market/orders/status</c> — 300 requests / minute.
    /// </summary>
    /// <param name="orderId">Nobitex order id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Order> GetOrderAsync(long orderId, CancellationToken cancellationToken = default)
        => GetOrderCoreAsync(new Dictionary<string, object?> { ["id"] = orderId }, cancellationToken);

    /// <summary>
    /// Returns the status of an order by the user supplied <c>clientOrderId</c>.
    /// Only searches among open/active/inactive orders (experimental endpoint feature).
    /// </summary>
    public Task<Order> GetOrderByClientOrderIdAsync(string clientOrderId, CancellationToken cancellationToken = default)
        => GetOrderCoreAsync(new Dictionary<string, object?> { ["clientOrderId"] = clientOrderId }, cancellationToken);

    /// <summary>
    /// Lists the orders of the user.
    /// Endpoint: <c>GET /market/orders/list</c> — 30 requests / minute, 100 orders per page by default.
    /// </summary>
    /// <param name="request">Filter definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<OrderListResponse> GetOrdersAsync(OrderListRequest request, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/market/orders/list", request.ToQuery(), null,
            RateLimitGroups.OrderList, authenticated: true, cancellationToken).ConfigureAwait(false);

        return new OrderListResponse
        {
            Orders = node["orders"]?.Deserialize<List<Order>>(NobitexJson.Options) ?? new List<Order>(),
            Stats = node["stats"]?.Deserialize<OrderListStatistics>(NobitexJson.Options),
            HasNext = node["hasNext"]?.GetValue<bool>() ?? false,
        };
    }

    /// <summary>Convenience overload that lists open orders of a market.</summary>
    public Task<OrderListResponse> GetOpenOrdersAsync(
        string? srcCurrency = null, string? dstCurrency = null, CancellationToken cancellationToken = default)
        => GetOrdersAsync(new OrderListRequest
        {
            SrcCurrency = srcCurrency,
            DstCurrency = dstCurrency,
            Status = OrderStatusFilter.Open,
            Details = 2,
        }, cancellationToken);

    /// <summary>
    /// Cancels (or re-activates) an order.
    /// Endpoint: <c>POST /market/orders/update-status</c> — 90 requests / minute.
    /// Cancelling one leg of an OCO pair cancels both orders.
    /// </summary>
    public async Task<Order> CancelOrderAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Post, "/market/orders/update-status", null,
            new Dictionary<string, object?> { ["order"] = orderId, ["status"] = "canceled" },
            RateLimitGroups.CancelOrder, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["order"]?.Deserialize<Order>(NobitexJson.Options)
            ?? throw new NobitexApiException("EmptyResponse", "The order payload was empty.", 200);
    }

    /// <summary>Cancels an order using its client order id (experimental).</summary>
    public Task<Order> CancelOrderByClientOrderIdAsync(string clientOrderId, CancellationToken cancellationToken = default)
        => _client.SendAsync<Order>(
            HttpMethod.Post, "/market/orders/update-status", null,
            new Dictionary<string, object?> { ["clientOrderId"] = clientOrderId, ["status"] = "canceled" },
            RateLimitGroups.CancelOrder, authenticated: true, cancellationToken);

    /// <summary>
    /// Cancels many orders in one call.
    /// Endpoint: <c>POST /market/orders/cancel-batch</c>.
    /// </summary>
    public async Task<IReadOnlyList<long>> CancelOrdersAsync(IEnumerable<long> orderIds, CancellationToken cancellationToken = default)
    {
        var ids = orderIds?.ToList() ?? throw new ArgumentNullException(nameof(orderIds));
        var node = await _client.SendNodeAsync(
            HttpMethod.Post, "/market/orders/cancel-batch", null,
            new Dictionary<string, object?> { ["orderIds"] = ids },
            RateLimitGroups.CancelOrder, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["cancelledOrders"]?.Deserialize<List<long>>(NobitexJson.Options) ?? ids;
    }

    /// <summary>
    /// Cancels all active orders matching the filters.
    /// Endpoint: <c>POST /market/orders/cancel-old</c> — 30 requests / minute.
    /// Inactive (not yet triggered) non-OCO stop orders are not affected.
    /// </summary>
    /// <param name="hours">Only cancel orders created within the last N hours. <c>null</c> = all.</param>
    /// <param name="srcCurrency"></param>
    /// <param name="dstCurrency"></param>
    /// <param name="execution"></param>
    /// <param name="tradeType"></param>
    /// <param name="cancellationToken"></param>
    public Task<NobitexAcknowledge> CancelOldOrdersAsync(
        double? hours = null,
        string? srcCurrency = null,
        string? dstCurrency = null,
        OrderExecution? execution = null,
        TradeType? tradeType = null,
        CancellationToken cancellationToken = default)
        => _client.SendAsync<NobitexAcknowledge>(
            HttpMethod.Post, "/market/orders/cancel-old", null,
            new Dictionary<string, object?>
            {
                ["hours"] = hours,
                ["srcCurrency"] = srcCurrency,
                ["dstCurrency"] = dstCurrency,
                ["execution"] = execution is null ? null : NobitexSymbol.ToApiString(execution.Value),
                ["tradeType"] = tradeType is null ? null : NobitexSymbol.ToApiString(tradeType.Value),
            },
            RateLimitGroups.CancelOrder, authenticated: true, cancellationToken);

    /// <summary>
    /// Returns the user trades of the last 3 days.
    /// Endpoint: <c>GET /market/trades/list</c> — 30 requests / minute, 30 items per page.
    /// </summary>
    public async Task<MyTradesResponse> GetMyTradesAsync(
        string? srcCurrency = null,
        string? dstCurrency = null,
        long? fromId = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/market/trades/list",
            QueryBuilder.Build(new
            {
                srcCurrency,
                dstCurrency,
                fromId,
                page,
                pageSize,
            }),
            null, RateLimitGroups.OrderList, authenticated: true, cancellationToken).ConfigureAwait(false);

        return new MyTradesResponse
        {
            Trades = node["trades"]?.Deserialize<List<MyTrade>>(NobitexJson.Options) ?? new List<MyTrade>(),
            HasNext = node["hasNext"]?.GetValue<bool>() ?? false,
        };
    }

    private Task<Order> GetOrderCoreAsync(Dictionary<string, object?> payload, CancellationToken cancellationToken)
        => _client.SendAsync<Order>(
            HttpMethod.Post, "/market/orders/status", null, payload,
            RateLimitGroups.OrderStatus, authenticated: true, cancellationToken);

    private static Dictionary<string, object?> BuildPlacePayload(PlaceOrderRequest request) => new()
    {
        ["type"] = NobitexSymbol.ToApiString(request.Side),
        ["execution"] = NobitexSymbol.ToApiString(request.Execution),
        ["srcCurrency"] = request.SrcCurrency,
        ["dstCurrency"] = request.DstCurrency,
        ["amount"] = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["price"] = request.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["stopPrice"] = request.StopPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["stopLimitPrice"] = request.StopLimitPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["mode"] = request.Mode,
        ["clientOrderId"] = request.ClientOrderId,
        ["pro"] = request.ProMode ? "yes" : null,
    };
}