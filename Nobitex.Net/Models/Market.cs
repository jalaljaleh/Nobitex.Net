using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nobitex.Net;


/// <summary>
/// Wrapper response for GET /v2/trades/{symbol}
/// </summary>
public record TradesHistoryResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("trades")] List<TradeHistoryDto>? Trades
);

/// <summary>
/// Single trade entry returned by the trades endpoint.
/// - time: unix milliseconds
/// - price/volume: strings to preserve precision
/// - type: "buy" or "sell"
/// </summary>
public record TradeHistoryDto(
    [property: JsonPropertyName("time")] long Time,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("type")] string? Type
);

/// <summary>
/// Wrapper response for GET /market/stats
/// The stats property maps market-key -> MarketStat. Example key: "btc-rls".
/// </summary>
public record MarketStatsResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("stats")] Dictionary<string, MarketStat>? Stats,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);

/// <summary>
/// Individual market statistics.
/// Monetary and numeric values are strings to preserve precision/formatting.
/// </summary>
public record MarketStat(
    [property: JsonPropertyName("isClosed")] bool? IsClosed,
    [property: JsonPropertyName("bestSell")] string? BestSell,
    [property: JsonPropertyName("bestBuy")] string? BestBuy,
    [property: JsonPropertyName("volumeSrc")] string? VolumeSrc,
    [property: JsonPropertyName("volumeDst")] string? VolumeDst,
    [property: JsonPropertyName("latest")] string? Latest,
    [property: JsonPropertyName("mark")] string? Mark,
    [property: JsonPropertyName("dayLow")] string? DayLow,
    [property: JsonPropertyName("dayHigh")] string? DayHigh,
    [property: JsonPropertyName("dayOpen")] string? DayOpen,
    [property: JsonPropertyName("dayClose")] string? DayClose,
    [property: JsonPropertyName("dayChange")] string? DayChange
);





/// <summary>
/// UDF history response (matches TradingView UDF shape).
/// s: "ok" | "error" | "no_data"
/// t: unix timestamps (seconds)
/// o,h,l,c: prices (strings to preserve precision or numbers if preferred)
/// v: volumes (strings recommended)
/// </summary>
public record UdfHistoryResponse(
    [property: JsonPropertyName("s")] string? Status,
    [property: JsonPropertyName("t")] List<long>? Timestamps,
    [property: JsonPropertyName("o")] List<string>? Open,
    [property: JsonPropertyName("h")] List<string>? High,
    [property: JsonPropertyName("l")] List<string>? Low,
    [property: JsonPropertyName("c")] List<string>? Close,
    [property: JsonPropertyName("v")] List<string>? Volume,
    [property: JsonPropertyName("errmsg")] string? ErrorMessage
);





/// <summary>
/// Response for GET /v3/orderbook/{symbol}
/// </summary>
public record OrderbookResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("lastUpdate")] long? LastUpdate,
    [property: JsonPropertyName("lastTradePrice")] string? LastTradePrice,
    [property: JsonPropertyName("asks")] List<OrderbookLevel>? Asks,
    [property: JsonPropertyName("bids")] List<OrderbookLevel>? Bids
);

/// <summary>
/// Compact level representation used by the API: [ price, amount ] both as strings.
/// Using a small DTO preserves readability while matching the API shape.
/// </summary>
public record OrderbookLevel(
    [property: JsonPropertyName("0")] string? Price,
    [property: JsonPropertyName("1")] string? Amount
);

/// <summary>
/// Summary object used when calling /v3/orderbook/all (each market maps to this).
/// </summary>
public record OrderbookSummary(
    [property: JsonPropertyName("lastUpdate")] long? LastUpdate,
    [property: JsonPropertyName("lastTradePrice")] string? LastTradePrice,
    [property: JsonPropertyName("asks")] List<OrderbookLevel>? Asks,
    [property: JsonPropertyName("bids")] List<OrderbookLevel>? Bids
);


/// <summary>
/// Response wrapper for GET /margin/markets/list
/// </summary>
public record MarginMarketsListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("markets")] IDictionary<string, MarginMarketSetting> Markets
);

/// <summary>
/// Settings for a single margin market
/// </summary>
public record MarginMarketSetting(
    [property: JsonPropertyName("srcCurrency")] string SrcCurrency,
    [property: JsonPropertyName("dstCurrency")] string DstCurrency,
    [property: JsonPropertyName("positionFeeRate")] string PositionFeeRate,
    [property: JsonPropertyName("maxLeverage")] string MaxLeverage,
    [property: JsonPropertyName("sellEnabled")] bool SellEnabled,
    [property: JsonPropertyName("buyEnabled")] bool BuyEnabled
);




/// <summary>
/// Response for GET /liquidity-pools/list
/// </summary>
public record LiquidityPoolsListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("pools")] IDictionary<string, LiquidityPoolInfo> Pools
);

/// <summary>
/// Per-currency liquidity pool info
/// </summary>
public record LiquidityPoolInfo(
    [property: JsonPropertyName("capacity")] string Capacity,
    [property: JsonPropertyName("filledCapacity")] string FilledCapacity
);