using System.Text.Json.Serialization;

namespace Nobitex.Net.Models;

/// <summary>Leverage fee rates of the user.</summary>
public sealed class MarginFeeRates
{
    /// <summary>Fee rate for 2x leverage.</summary>
    public decimal Leverage2 { get; set; }

    /// <summary>Fee rate for 3x leverage.</summary>
    public decimal Leverage3 { get; set; }

    /// <summary>Fee rate for 5x leverage.</summary>
    public decimal Leverage5 { get; set; }
}

/// <summary>Maximum borrowing/delegation limit for a market.</summary>
public sealed class DelegationLimit
{
    /// <summary>Source currency.</summary>
    public string? SrcCurrency { get; set; }

    /// <summary>Destination currency.</summary>
    public string? DstCurrency { get; set; }

    /// <summary>Maximum delegatable amount.</summary>
    public decimal Limit { get; set; }

    /// <summary>Amount already used.</summary>
    public decimal Used { get; set; }

    /// <summary>Remaining amount.</summary>
    public decimal Remaining => Math.Max(0m, Limit - Used);
}

/// <summary>An open margin position.</summary>
public sealed class MarginPosition
{
    /// <summary>Position id.</summary>
    public long Id { get; set; }

    /// <summary>Market symbol.</summary>
    public string? Market { get; set; }

    /// <summary>Position direction: <c>long</c> or <c>short</c>.</summary>
    public string? Side { get; set; }

    /// <summary>Leverage used.</summary>
    public decimal Leverage { get; set; }

    /// <summary>Position size.</summary>
    public decimal Amount { get; set; }

    /// <summary>Collateral (margin) locked for the position.</summary>
    public decimal Collateral { get; set; }

    /// <summary>Entry price.</summary>
    [JsonPropertyName("entryPrice")]
    public decimal EntryPrice { get; set; }

    /// <summary>Mark price used for the PnL calculation.</summary>
    [JsonPropertyName("markPrice")]
    public decimal MarkPrice { get; set; }

    /// <summary>Liquidation price.</summary>
    [JsonPropertyName("liquidationPrice")]
    public decimal LiquidationPrice { get; set; }

    /// <summary>Unrealised profit/loss.</summary>
    [JsonPropertyName("unrealizedPnl")]
    public decimal UnrealizedPnl { get; set; }

    /// <summary>Position status.</summary>
    public string? Status { get; set; }

    /// <summary>Opening time.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Distance to the liquidation price in percent.</summary>
    public decimal LiquidationDistancePercent
        => MarkPrice == 0 ? 0 : Math.Abs(MarkPrice - LiquidationPrice) / MarkPrice * 100m;
}