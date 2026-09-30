using Nobitex.Net.Models;
using System.Text;

namespace Nobitex.Net.Internal;

/// <summary>
/// Builds a URL encoded query string from an anonymous object, skipping <c>null</c> values.
/// Monetary values are rendered with the invariant culture so no thousands separators are emitted.
/// </summary>
internal static class QueryBuilder
{
    public static string Build(object? parameters)
    {
        if (parameters is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var first = true;

        foreach (var property in parameters.GetType().GetProperties())
        {
            var value = property.GetValue(parameters);
            if (value is null)
            {
                continue;
            }

            var rendered = value switch
            {
                bool b => b ? "yes" : "no",
                Enum e => e.ToString().ToLowerInvariant(),
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            };

            if (string.IsNullOrEmpty(rendered))
            {
                continue;
            }

            sb.Append(first ? string.Empty : '&');
            sb.Append(Uri.EscapeDataString(ToCamelCase(property.Name)));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(rendered));
            first = false;
        }

        return sb.ToString();
    }

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}

/// <summary>Helpers for Nobitex market symbols (<c>BTCIRT</c>, <c>ETHUSDT</c>, ...) and enum wire formats.</summary>
public static class NobitexSymbol
{
    /// <summary>Normalises a user supplied symbol to upper-case (e.g. <c>btcirt</c> → <c>BTCIRT</c>).</summary>
    public static string Normalize(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        return symbol.Trim().ToUpperInvariant();
    }

    /// <summary>Builds a symbol from its two legs, e.g. <c>("btc", "rls")</c> → <c>BTCIRT</c>.</summary>
    public static string FromPair(string srcCurrency, string dstCurrency)
    {
        var dst = dstCurrency?.ToLowerInvariant() switch
        {
            "rls" or "irr" or "تومان" or "toman" => "IRT",
            _ => (dstCurrency ?? string.Empty).ToUpperInvariant(),
        };

        return $"{srcCurrency.ToUpperInvariant()}{dst}";
    }

    /// <summary>
    /// Renders an enum the way the Nobitex API expects it in the request body,
    /// for example <c>buy</c>, <c>limit</c>, <c>stop_market</c> or <c>spot</c>.
    /// </summary>
    public static string ToApiString<T>(T value) where T : Enum
        => value switch
        {
            OrderExecution execution => execution switch
            {
                OrderExecution.StopMarket => "stop_market",
                OrderExecution.StopLimit => "stop_limit",
                _ => execution.ToString().ToLowerInvariant(),
            },
            _ => value.ToString().ToLowerInvariant(),
        };

    /// <summary>Maps a <see cref="CandleResolution"/> to the TradingView resolution string.</summary>
    public static string ToResolution(CandleResolution resolution) => resolution switch
    {
        CandleResolution.Minute1 => "1",
        CandleResolution.Minute5 => "5",
        CandleResolution.Minute15 => "15",
        CandleResolution.Minute30 => "30",
        CandleResolution.Hour1 => "60",
        CandleResolution.Hour3 => "180",
        CandleResolution.Hour4 => "240",
        CandleResolution.Hour6 => "360",
        CandleResolution.Hour12 => "720",
        CandleResolution.Day1 => "D",
        CandleResolution.Day2 => "2D",
        CandleResolution.Day3 => "3D",
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null),
    };
}