using System.Text.Json.Serialization;

namespace Nobitex.Net.Models;

/// <summary>
/// Definition of an order to be placed on Nobitex. Use the static factory helpers
/// (<see cref="Limit"/>, <see cref="Market"/>, <see cref="StopMarket"/>, <see cref="Oco"/>)
/// or set the properties directly.
/// </summary>
public sealed class PlaceOrderRequest
{
    /// <summary>Buy or sell.</summary>
    public OrderSide Side { get; set; } = OrderSide.Buy;

    /// <summary>Execution mode. Defaults to <see cref="OrderExecution.Limit"/>.</summary>
    public OrderExecution Execution { get; set; } = OrderExecution.Limit;

    /// <summary>Source currency, e.g. <c>btc</c>. The <c>amount</c> is expressed in this currency.</summary>
    public string SrcCurrency { get; set; } = string.Empty;

    /// <summary>Destination currency, e.g. <c>rls</c> or <c>usdt</c>.</summary>
    public string DstCurrency { get; set; } = string.Empty;

    /// <summary>Order size in <see cref="SrcCurrency"/> units.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Unit price. Mandatory for limit and stop-limit orders. For market orders it is optional but
    /// strongly recommended as a protective price band.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>Stop (trigger) price. Mandatory for <see cref="OrderExecution.StopMarket"/> and OCO orders.</summary>
    public decimal? StopPrice { get; set; }

    /// <summary>Limit price of the stop-limit leg of an OCO order.</summary>
    public decimal? StopLimitPrice { get; set; }

    /// <summary>Set to <c>"oco"</c> to create an OCO order.</summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Optional user defined id (max 32 characters). Must be unique among the user's open orders.
    /// Recommended to protect against duplicated orders caused by network retries.
    /// </summary>
    public string? ClientOrderId { get; set; }

    /// <summary>Enables "Pro" mode which disables some client protection restrictions.</summary>
    public bool ProMode { get; set; }

    /// <summary>Set to <c>true</c> when the order targets the margin market.</summary>
    public bool IsMargin { get; set; }

    /// <summary>Leverage for margin orders (e.g. 2, 3, 5).</summary>
    public int? Leverage { get; set; }

    /// <summary>Direction of the margin position (<c>long</c> / <c>short</c>).</summary>
    public string? PositionSide { get; set; }

    /// <summary>Total order value in the destination currency.</summary>
    public decimal TotalValue => Execution is OrderExecution.Market or OrderExecution.StopMarket ? 0m : Amount * (Price ?? 0m);

    /// <summary>Creates a limit order definition.</summary>
    public static PlaceOrderRequest Limit(OrderSide side, string src, string dst, decimal amount, decimal price, string? clientOrderId = null)
        => new() { Side = side, Execution = OrderExecution.Limit, SrcCurrency = src, DstCurrency = dst, Amount = amount, Price = price, ClientOrderId = clientOrderId };

    /// <summary>Creates a market order definition.</summary>
    public static PlaceOrderRequest Market(OrderSide side, string src, string dst, decimal amount, decimal? expectedPrice = null, string? clientOrderId = null)
        => new() { Side = side, Execution = OrderExecution.Market, SrcCurrency = src, DstCurrency = dst, Amount = amount, Price = expectedPrice, ClientOrderId = clientOrderId };

    /// <summary>Creates a stop-market (stop loss / stop entry) definition.</summary>
    public static PlaceOrderRequest StopMarket(OrderSide side, string src, string dst, decimal amount, decimal stopPrice, string? clientOrderId = null)
        => new() { Side = side, Execution = OrderExecution.StopMarket, SrcCurrency = src, DstCurrency = dst, Amount = amount, StopPrice = stopPrice, ClientOrderId = clientOrderId };

    /// <summary>Creates an OCO definition (limit + stop-limit in a single request).</summary>
    public static PlaceOrderRequest Oco(OrderSide side, string src, string dst, decimal amount, decimal price, decimal stopPrice, decimal stopLimitPrice, string? clientOrderId = null)
        => new() { Side = side, Execution = OrderExecution.Limit, Mode = "oco", SrcCurrency = src, DstCurrency = dst, Amount = amount, Price = price, StopPrice = stopPrice, StopLimitPrice = stopLimitPrice, ClientOrderId = clientOrderId };

    /// <summary>
    /// Validates the request locally before it is sent, so obvious mistakes never reach the exchange.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a mandatory field is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric field is not positive.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SrcCurrency))
        {
            throw new ArgumentException("SrcCurrency is required.", nameof(SrcCurrency));
        }

        if (string.IsNullOrWhiteSpace(DstCurrency))
        {
            throw new ArgumentException("DstCurrency is required.", nameof(DstCurrency));
        }

        if (Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "Amount must be greater than zero.");
        }

        if (ClientOrderId is { Length: > 32 })
        {
            throw new ArgumentException("ClientOrderId must not exceed 32 characters.", nameof(ClientOrderId));
        }

        switch (Execution)
        {
            case OrderExecution.Limit when Price is null or <= 0:
                throw new ArgumentException("A limit order requires a positive Price.");
            case OrderExecution.StopMarket when StopPrice is null or <= 0:
                throw new ArgumentException("A stop-market order requires a positive StopPrice.");
            case OrderExecution.StopLimit when StopPrice is null or <= 0 || Price is null or <= 0:
                throw new ArgumentException("A stop-limit order requires StopPrice and Price.");
        }

        if (Mode == "oco" && (StopPrice is null || StopLimitPrice is null || Price is null))
        {
            throw new ArgumentException("An OCO order requires Price, StopPrice and StopLimitPrice.");
        }

        if (IsMargin && Leverage is not (null or > 1))
        {
            throw new ArgumentOutOfRangeException(nameof(Leverage), Leverage, "Leverage must be greater than 1.");
        }
    }
}

/// <summary>An order as returned by Nobitex.</summary>
public sealed class Order
{
    /// <summary>Nobitex order id.</summary>
    public long Id { get; set; }

    /// <summary>User supplied client order id.</summary>
    [JsonPropertyName("clientOrderId")]
    public string? ClientOrderId { get; set; }

    /// <summary>Order side.</summary>
    public OrderSide Type { get; set; }

    /// <summary>Execution mode.</summary>
    public OrderExecution Execution { get; set; }

    /// <summary>Spot or margin.</summary>
    public TradeType TradeType { get; set; } = TradeType.Spot;

    /// <summary>Market symbol, e.g. <c>BTC-IRT</c>.</summary>
    public string? Market { get; set; }

    /// <summary>Source currency name.</summary>
    [JsonPropertyName("srcCurrency")]
    public string SrcCurrency { get; set; } = string.Empty;

    /// <summary>Destination currency name.</summary>
    [JsonPropertyName("dstCurrency")]
    public string DstCurrency { get; set; } = string.Empty;

    /// <summary>Unit price (<c>market</c> for market orders).</summary>
    public decimal Price { get; set; }

    /// <summary>Trigger price of stop orders.</summary>
    [JsonPropertyName("param1")]
    public decimal? StopPrice { get; set; }

    /// <summary>Order size.</summary>
    public decimal Amount { get; set; }

    /// <summary>Total order value.</summary>
    [JsonPropertyName("totalOrderPrice")]
    public decimal TotalOrderPrice { get; set; }

    /// <summary>Filled volume.</summary>
    [JsonPropertyName("matchedAmount")]
    public decimal MatchedAmount { get; set; }

    /// <summary>Remaining volume.</summary>
    [JsonPropertyName("unmatchedAmount")]
    public decimal UnmatchedAmount { get; set; }

    /// <summary>Average execution price.</summary>
    [JsonPropertyName("averagePrice")]
    public decimal AveragePrice { get; set; }

    /// <summary>Accumulated fee.</summary>
    public decimal Fee { get; set; }

    /// <summary>Is the order partially filled?</summary>
    public bool Partial { get; set; }

    /// <summary>Current status.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>Creation time (UTC).</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Id of the sibling order for OCO pairs.</summary>
    [JsonPropertyName("pairId")]
    public long? PairId { get; set; }

    /// <summary>Execution progress between 0 and 1.</summary>
    public double Progress => Amount == 0 ? 0 : Math.Min(1d, (double)(MatchedAmount / Amount));

    /// <summary>Indicates whether the order can still be filled.</summary>
    public bool IsOpen => Status is OrderStatus.New or OrderStatus.Active or OrderStatus.Inactive;
}

/// <summary>Filter definition for <c>GET /market/orders/list</c>.</summary>
public sealed class OrderListRequest
{
    /// <summary>Status filter. Default is <see cref="OrderStatusFilter.Open"/>.</summary>
    public OrderStatusFilter Status { get; set; } = OrderStatusFilter.Open;

    /// <summary>Side filter. <c>null</c> = both.</summary>
    public OrderSide? Type { get; set; }

    /// <summary>Execution filter.</summary>
    public OrderExecution? Execution { get; set; }

    /// <summary>Spot or margin.</summary>
    public TradeType? TradeType { get; set; }

    /// <summary>Source currency.</summary>
    public string? SrcCurrency { get; set; }

    /// <summary>Destination currency.</summary>
    public string? DstCurrency { get; set; }

    /// <summary>Level of detail: 1 (default) or 2 (adds id, status, fee, created_at, averagePrice).</summary>
    public int Details { get; set; } = 2;

    /// <summary>Only return orders with an id greater than this value. Cannot be combined with <see cref="Page"/>.</summary>
    public long? FromId { get; set; }

    /// <summary>Page number (1 based).</summary>
    public int? Page { get; set; }

    /// <summary>Page size, up to 1000.</summary>
    public int? PageSize { get; set; }

    /// <summary>Sort key, e.g. <c>id</c>, <c>-created_at</c>, <c>price</c>.</summary>
    public string? Order { get; set; }

    internal string ToQuery() => Nobitex.Net.Internal.QueryBuilder.Build(new
    {
        Status = Status == OrderStatusFilter.Open ? "open" : Status.ToString().ToLowerInvariant(),
        Type,
        Execution = Execution?.ToString()?.ToLowerInvariant(),
        TradeType = TradeType?.ToString()?.ToLowerInvariant(),
        SrcCurrency,
        DstCurrency,
        Details,
        FromId,
        Page,
        PageSize,
        Order,
    });
}

/// <summary>Response of <c>GET /market/orders/list</c>.</summary>
public sealed class OrderListResponse
{
    /// <summary>Returned orders.</summary>
    public List<Order> Orders { get; set; } = new();

    /// <summary>Aggregated statistics of the query.</summary>
    public OrderListStatistics? Stats { get; set; }

    /// <summary>Indicates whether more pages are available.</summary>
    public bool HasNext { get; set; }
}

/// <summary>Aggregate statistics returned together with an order list.</summary>
public sealed class OrderListStatistics
{
    /// <summary>Number of active orders.</summary>
    public int ActiveCount { get; set; }

    /// <summary>Number of done orders.</summary>
    public int DoneCount { get; set; }

    /// <summary>Total number of matching orders.</summary>
    public int TotalCount { get; set; }

    /// <summary>Average price of the orders.</summary>
    public decimal AveragePrice { get; set; }

    /// <summary>Average price expressed in USDT.</summary>
    public decimal AveragePriceUsdt { get; set; }

    /// <summary>Total value of the orders.</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Total value expressed in USDT.</summary>
    public decimal TotalPriceUsdt { get; set; }
}

/// <summary>A trade of the authenticated user (<c>GET /market/trades/list</c>).</summary>
public sealed class MyTrade
{
    /// <summary>Trade id.</summary>
    public long Id { get; set; }

    /// <summary>Id of the order that produced this trade.</summary>
    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }

    /// <summary>Market symbol, e.g. <c>USDT-RLS</c>.</summary>
    public string? Market { get; set; }

    /// <summary>Source currency.</summary>
    [JsonPropertyName("srcCurrency")]
    public string SrcCurrency { get; set; } = string.Empty;

    /// <summary>Destination currency.</summary>
    [JsonPropertyName("dstCurrency")]
    public string DstCurrency { get; set; } = string.Empty;

    /// <summary>Execution time (UTC).</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Side of the trade.</summary>
    public OrderSide Type { get; set; }

    /// <summary>Execution price.</summary>
    public decimal Price { get; set; }

    /// <summary>Executed volume.</summary>
    public decimal Amount { get; set; }

    /// <summary>Total value of the trade.</summary>
    public decimal Total { get; set; }

    /// <summary>Fee paid for the trade.</summary>
    public decimal Fee { get; set; }
}

/// <summary>Response of <c>GET /market/trades/list</c>.</summary>
public sealed class MyTradesResponse
{
    /// <summary>User trades.</summary>
    public List<MyTrade> Trades { get; set; } = new();

    /// <summary>Indicates whether more trades are available.</summary>
    public bool HasNext { get; set; }
}

/// <summary>Generic success acknowledgement (<c>status: ok</c>).</summary>
public sealed class NobitexAcknowledge
{
    /// <summary>Always <c>ok</c> when the call succeeded.</summary>
    public string Status { get; set; } = "ok";
}