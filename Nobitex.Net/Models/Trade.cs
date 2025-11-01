using System.Text.Json.Serialization;
namespace Nobitex.Net;




/// <summary>
/// Unified order request capable of expressing normal/market/stop/oco orders.
/// </summary>
public record AddOrderRequest(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("execution")] string? Execution,
    [property: JsonPropertyName("srcCurrency")] string SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string DstCurrency,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("clientOrderId")] string? ClientOrderId,
    // stop / oco specific
    [property: JsonPropertyName("stopPrice")] string? StopPrice,
    [property: JsonPropertyName("stopLimitPrice")] string? StopLimitPrice,
    [property: JsonPropertyName("mode")] string? Mode
);


/// <summary>
/// Response for POST /market/orders/add
/// </summary>
public record OrderAddResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("order")] OrderDto? Order
);

/// <summary>
/// Unified Order DTO used across spot and margin endpoints (placement, status, list, margin).
/// Monetary values are strings to preserve API formatting and precision.
/// Most fields are nullable because different endpoints/level-of-detail return different subsets.
/// </summary>
public record OrderDto(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("execution")] string? Execution,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("tradeType")] string? TradeType,
    [property: JsonPropertyName("market")] string? Market,
    [property: JsonPropertyName("srcCurrency")] string? SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string? DstCurrency,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("matchedAmount")] string? MatchedAmount,
    [property: JsonPropertyName("unmatchedAmount")] string? UnmatchedAmount,
    [property: JsonPropertyName("totalPrice")] string? TotalPrice,
    [property: JsonPropertyName("totalOrderPrice")] string? TotalOrderPrice,
    [property: JsonPropertyName("averagePrice")] string? AveragePrice,
    [property: JsonPropertyName("fee")] string? Fee,
    [property: JsonPropertyName("partial")] bool? Partial,
    [property: JsonPropertyName("leverage")] string? Leverage,
    [property: JsonPropertyName("side")] string? Side,
    [property: JsonPropertyName("param1")] string? Param1,
    [property: JsonPropertyName("pairId")] long? PairId,
    [property: JsonPropertyName("clientOrderId")] string? ClientOrderId,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("isMyOrder")] bool? IsMyOrder
);

/// <summary>
/// Request payload for POST /market/orders/status
/// Provide at least one of Id or ClientOrderId.
/// </summary>
public record OrderStatusRequest(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("clientOrderId")] string? ClientOrderId
);

/// <summary>
/// Response for POST /market/orders/status
/// </summary>
public record OrderStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("order")] OrderStatusDto? Order
);



/// <summary>
/// Unified order DTO used for status, update-status and list responses.
/// Contains the superset of fields returned by different endpoints; most fields are nullable.
/// Monetary values are strings to preserve API formatting and precision.
/// </summary>
public record OrderStatusDto(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("execution")] string? Execution,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("tradeType")] string? TradeType,
    [property: JsonPropertyName("market")] string? Market,
    [property: JsonPropertyName("srcCurrency")] string? SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string? DstCurrency,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("matchedAmount")] string? MatchedAmount,
    [property: JsonPropertyName("unmatchedAmount")] string? UnmatchedAmount,
    [property: JsonPropertyName("totalPrice")] string? TotalPrice,
    [property: JsonPropertyName("totalOrderPrice")] string? TotalOrderPrice,
    [property: JsonPropertyName("averagePrice")] string? AveragePrice,
    [property: JsonPropertyName("fee")] string? Fee,
    [property: JsonPropertyName("partial")] bool? Partial,
    [property: JsonPropertyName("param1")] string? Param1,
    [property: JsonPropertyName("pairId")] long? PairId,
    [property: JsonPropertyName("clientOrderId")] string? ClientOrderId,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("isMyOrder")] bool? IsMyOrder
);







/// <summary>
/// Response wrapper for orders list
/// </summary>
public record OrdersListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("orders")] IReadOnlyList<OrderListItem>? Orders
);

/// <summary>
/// Order item returned in /market/orders/list.
/// Fields returned depend on details parameter (1 or 2).
/// Monetary values are strings to preserve precision/formatting.
/// </summary>
public record OrderListItem(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("execution")] string? Execution,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("tradeType")] string? TradeType,
    [property: JsonPropertyName("srcCurrency")] string? SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string? DstCurrency,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("matchedAmount")] string? MatchedAmount,
    [property: JsonPropertyName("averagePrice")] string? AveragePrice,
    [property: JsonPropertyName("fee")] string? Fee,
    [property: JsonPropertyName("clientOrderId")] string? ClientOrderId,
    // optional fields that may appear when details=2
    [property: JsonPropertyName("param1")] string? Param1,
    [property: JsonPropertyName("totalPrice")] string? TotalPrice,
    [property: JsonPropertyName("totalOrderPrice")] string? TotalOrderPrice,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("pairId")] long? PairId
);







/// <summary>
/// Request payload for POST /market/orders/update-status
/// Provide at least one of Order (id) or ClientOrderId and the new Status.
/// </summary>
public record UpdateOrderStatusRequest(
    [property: JsonPropertyName("order")] long? Order,
    [property: JsonPropertyName("clientOrderId")] string? ClientOrderId,
    [property: JsonPropertyName("status")] string Status
);

/// <summary>
/// Response for POST /market/orders/update-status
/// </summary>
public record UpdateOrderStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("updatedStatus")] string? UpdatedStatus,
    [property: JsonPropertyName("order")] OrderStatusDto? Order
);




/// <summary>
/// Request payload for POST /market/orders/cancel-old
/// Provide any combination of filters; omit hours to cancel all matching active orders.
/// </summary>
public record CancelOldRequest(
    [property: JsonPropertyName("hours")] double? Hours,
    [property: JsonPropertyName("execution")] string? Execution,
    [property: JsonPropertyName("tradeType")] string? TradeType,
    [property: JsonPropertyName("srcCurrency")] string? SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string? DstCurrency
);

/// <summary>
/// Generic simple response for endpoints that return only status (or failed).
/// </summary>
public record CancelOldResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);










/// <summary>
/// Response wrapper for GET /market/trades/list
/// </summary>
public record TradesListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("trades")] IReadOnlyList<TradeDto>? Trades,
    [property: JsonPropertyName("hasNext")] bool? HasNext
);

/// <summary>
/// Individual trade item returned by GET /market/trades/list
/// Monetary values are strings to preserve API formatting and precision.
/// </summary>
public record TradeDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("orderId")] long? OrderId,
    [property: JsonPropertyName("srcCurrency")] string? SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string? DstCurrency,
    [property: JsonPropertyName("market")] string? Market,
    [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("total")] string? Total,
    [property: JsonPropertyName("fee")] string? Fee
);














/// <summary>
/// Request payload for POST /margin/orders/add
/// Unified for single and OCO margin orders.
/// Monetary values are strings to preserve precision.
/// </summary>
public record MarginOrderRequest(
    [property: JsonPropertyName("execution")] string? Execution,           // "limit" | "market" | "stop_limit" | "stop_market"
    [property: JsonPropertyName("mode")] string? Mode,                   // "oco" for OCO
    [property: JsonPropertyName("srcCurrency")] string SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string DstCurrency,
    [property: JsonPropertyName("type")] string? Type,                   // "sell" | "buy"
    [property: JsonPropertyName("leverage")] string? Leverage,           // e.g., "2"
    [property: JsonPropertyName("amount")] string Amount,               // required
    [property: JsonPropertyName("price")] string? Price,                 // required for limit orders
    [property: JsonPropertyName("stopPrice")] string? StopPrice,         // stop trigger for stop orders / OCO
    [property: JsonPropertyName("stopLimitPrice")] string? StopLimitPrice,// stop limit price for OCO / stop_limit
    [property: JsonPropertyName("clientOrderId")] string? ClientOrderId
);

/// <summary>
/// Response wrapper for POST /margin/orders/add
/// For single order returns "order"; for OCO returns "orders".
/// </summary>
public record MarginOrderResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("order")] OrderDto? Order,
    [property: JsonPropertyName("orders")] OrderDto[]? Orders,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);





/// <summary>
/// Response wrapper for GET /positions/list
/// </summary>
public record PositionsListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("positions")] IReadOnlyList<PositionDto>? Positions,
    [property: JsonPropertyName("hasNext")] bool? HasNext
);

    /// <summary>
    /// Position DTO used for both positions list and single-position responses.
    /// Monetary fields are strings to preserve API formatting and precision.
    /// Fields that apply only to active or past positions are nullable.
    /// </summary>
    public record PositionDto(
        [property: JsonPropertyName("id")] long? Id,
        [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("srcCurrency")] string? SrcCurrency,
        [property: JsonPropertyName("dstCurrency")] string? DstCurrency,
        [property: JsonPropertyName("side")] string? Side,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("marginType")] string? MarginType,
        [property: JsonPropertyName("collateral")] string? Collateral,
        [property: JsonPropertyName("leverage")] string? Leverage,
        [property: JsonPropertyName("openedAt")] DateTimeOffset? OpenedAt,
        [property: JsonPropertyName("closedAt")] DateTimeOffset? ClosedAt,
        [property: JsonPropertyName("liquidationPrice")] string? LiquidationPrice,
        [property: JsonPropertyName("entryPrice")] string? EntryPrice,
        [property: JsonPropertyName("exitPrice")] string? ExitPrice,
        [property: JsonPropertyName("delegatedAmount")] string? DelegatedAmount,
        [property: JsonPropertyName("liability")] string? Liability,
        [property: JsonPropertyName("totalAsset")] string? TotalAsset,
        [property: JsonPropertyName("marginRatio")] string? MarginRatio,
        [property: JsonPropertyName("liabilityInOrder")] string? LiabilityInOrder,
        [property: JsonPropertyName("assetInOrder")] string? AssetInOrder,
        [property: JsonPropertyName("unrealizedPNL")] string? UnrealizedPNL,
        [property: JsonPropertyName("unrealizedPNLPercent")] string? UnrealizedPNLPercent,
        [property: JsonPropertyName("expirationDate")] string? ExpirationDate,
        [property: JsonPropertyName("extensionFee")] string? ExtensionFee,
        [property: JsonPropertyName("markPrice")] string? MarkPrice,
        [property: JsonPropertyName("PNL")] string? PNL,
        [property: JsonPropertyName("PNLPercent")] string? PNLPercent
    );

/// <summary>
/// Response for GET /positions/{positionId}/status
/// </summary>
public record PositionStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("position")] PositionDto? Position,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);





/// <summary>
/// Request payload for POST /positions/{positionId}/close
/// Supports single close and OCO (Mode = "oco").
/// Monetary fields are strings to preserve formatting and precision.
/// </summary>
public record ClosePositionRequest(
    [property: JsonPropertyName("execution")] string? Execution,         // "limit" | "market" | "stop_limit" | "stop_market"
    [property: JsonPropertyName("mode")] string? Mode,                 // "oco" for OCO closes
    [property: JsonPropertyName("amount")] string? Amount,             // required for non-OCO closes
    [property: JsonPropertyName("price")] string? Price,               // required for limit close; required in OCO
    [property: JsonPropertyName("stopPrice")] string? StopPrice,       // OCO / stop trigger
    [property: JsonPropertyName("stopLimitPrice")] string? StopLimitPrice // OCO / stop limit price
);

/// <summary>
/// Response for POST /positions/{positionId}/close
/// Returns a single Order for normal closes or Orders[] for OCO.
/// </summary>
public record ClosePositionResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("order")] OrderDto? Order,
    [property: JsonPropertyName("orders")] OrderDto[]? Orders,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);







/// <summary>
/// Request payload for POST /positions/{positionId}/edit-collateral
/// Monetary fields are strings to preserve formatting and precision.
/// </summary>
public record EditCollateralRequest(
    [property: JsonPropertyName("collateral")] string Collateral
);

/// <summary>
/// Response for POST /positions/{positionId}/edit-collateral
/// Returns updated position object.
/// </summary>
public record EditCollateralResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("position")] PositionDto? Position,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);

