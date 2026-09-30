using System.Text.Json.Serialization;

namespace Nobitex.Net.Models;

/// <summary>Side of an order.</summary>
public enum OrderSide
{
    /// <summary>Buy order.</summary>
    Buy,

    /// <summary>Sell order.</summary>
    Sell,
}

/// <summary>How an order is executed by the matching engine.</summary>
public enum OrderExecution
{
    /// <summary>Fills at the given price or better (default).</summary>
    Limit,

    /// <summary>Fills immediately at the best available price.</summary>
    Market,

    /// <summary>Becomes active when the market reaches the stop price, then fills as a limit order.</summary>
    StopLimit,

    /// <summary>Becomes active when the market reaches the stop price, then fills at market price.</summary>
    StopMarket,
}

/// <summary>Lifecycle status of an order as reported by Nobitex.</summary>
public enum OrderStatus
{
    /// <summary>The order was accepted and is waiting for validation.</summary>
    New,

    /// <summary>The order is in the order book and has unmatched volume.</summary>
    Active,

    /// <summary>The stop order has not reached its trigger price yet.</summary>
    Inactive,

    /// <summary>The order is fully filled.</summary>
    Done,

    /// <summary>The order was cancelled before being fully filled.</summary>
    Canceled,

    /// <summary>An unknown status (forward compatibility).</summary>
    Unknown,
}

/// <summary>Filter values accepted by <c>GET /market/orders/list</c>.</summary>
public enum OrderStatusFilter
{
    /// <summary>Every order.</summary>
    All,

    /// <summary>Open orders (active + inactive).</summary>
    Open,

    /// <summary>Partially or fully filled orders.</summary>
    Done,

    /// <summary>Filled or cancelled orders.</summary>
    Close,
}

/// <summary>Type of the market.</summary>
public enum TradeType
{
    /// <summary>Spot market.</summary>
    Spot,

    /// <summary>Margin (leverage) market.</summary>
    Margin,
}

/// <summary>Side of a public trade.</summary>
public enum TradeSide
{
    /// <summary>Buyer was the taker.</summary>
    Buy,

    /// <summary>Seller was the taker.</summary>
    Sell,
}

/// <summary>Candle intervals supported by <c>GET /market/udf/history</c>.</summary>
public enum CandleResolution
{
    /// <summary>1 minute.</summary>
    Minute1,

    /// <summary>5 minutes.</summary>
    Minute5,

    /// <summary>15 minutes.</summary>
    Minute15,

    /// <summary>30 minutes.</summary>
    Minute30,

    /// <summary>1 hour.</summary>
    Hour1,

    /// <summary>3 hours.</summary>
    Hour3,

    /// <summary>4 hours.</summary>
    Hour4,

    /// <summary>6 hours.</summary>
    Hour6,

    /// <summary>12 hours.</summary>
    Hour12,

    /// <summary>1 day.</summary>
    Day1,

    /// <summary>2 days.</summary>
    Day2,

    /// <summary>3 days.</summary>
    Day3,
}

/// <summary>Permissions of a Nobitex API key.</summary>
[Flags]
public enum ApiKeyPermissions
{
    /// <summary>Read only access (no database changes).</summary>
    None = 0,

    /// <summary>Allows querying balances, orders, trades, wallets and portfolio data.</summary>
    Read = 1,

    /// <summary>Allows placing, updating and cancelling orders.</summary>
    Trade = 2,

    /// <summary>Allows withdrawals. Always combine with an IP whitelist.</summary>
    Withdraw = 4,
}

/// <summary>Static extensions for the library enums.</summary>
public static class EnumExtensions
{
    /// <summary>Serialises <see cref="ApiKeyPermissions"/> as the comma separated value expected by Nobitex.</summary>
    public static string ToApiString(this ApiKeyPermissions permissions)
    {
        var parts = new List<string>();

        if (permissions.HasFlag(ApiKeyPermissions.Read))
        {
            parts.Add("READ");
        }

        if (permissions.HasFlag(ApiKeyPermissions.Trade))
        {
            parts.Add("TRADE");
        }

        if (permissions.HasFlag(ApiKeyPermissions.Withdraw))
        {
            parts.Add("WITHDRAW");
        }

        return parts.Count == 0 ? "READ" : string.Join(',', parts);
    }

    /// <summary>Parses the Nobitex permission list into the flags enum.</summary>
    public static ApiKeyPermissions ParsePermissions(string? value)
    {
        var result = ApiKeyPermissions.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= part.ToUpperInvariant() switch
            {
                "READ" => ApiKeyPermissions.Read,
                "TRADE" => ApiKeyPermissions.Trade,
                "WITHDRAW" => ApiKeyPermissions.Withdraw,
                _ => ApiKeyPermissions.None,
            };
        }

        return result;
    }
}