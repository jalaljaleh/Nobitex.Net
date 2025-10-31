using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nobitex.Net;
public record MarketStat(
    bool IsClosed,
    string BestSell,
    string BestBuy,
    string VolumeSrc,
    string VolumeDst,
    string Latest,
    string Mark,
    string DayLow,
    string DayHigh,
    string DayOpen,
    string DayClose,
    string DayChange
);

public record MarketStats(string Status, IDictionary<string, MarketStat> Stats);



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