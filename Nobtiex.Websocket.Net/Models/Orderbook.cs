namespace Nobitex.Websocket.Net
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents an orderbook snapshot as provided by Nobitex websocket orderbook channel.
    /// asks: list of [price, amount] as strings
    /// bids: list of [price, amount] as strings
    /// lastTradePrice: string
    /// lastUpdate: unix ms timestamp
    /// </summary>
    public sealed class Orderbook
    {
        public IReadOnlyList<(decimal Price, decimal Amount)> Asks { get; init; } = Array.Empty<(decimal, decimal)>();
        public IReadOnlyList<(decimal Price, decimal Amount)> Bids { get; init; } = Array.Empty<(decimal, decimal)>();
        public decimal? LastTradePrice { get; init; }
        public DateTimeOffset? LastUpdate { get; init; }

        public string Market { get; init; } = string.Empty;

        public static IReadOnlyList<(decimal Price, decimal Amount)> ParseEntries(System.Text.Json.JsonElement arr)
        {
            if (arr.ValueKind != System.Text.Json.JsonValueKind.Array) return Array.Empty<(decimal, decimal)>();
            var list = new List<(decimal, decimal)>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                var enumerator = item.EnumerateArray();
                if (!enumerator.MoveNext()) continue;
                var priceEl = enumerator.Current;
                if (!enumerator.MoveNext()) continue;
                var amountEl = enumerator.Current;

                if (priceEl.ValueKind == System.Text.Json.JsonValueKind.String &&
                    amountEl.ValueKind == System.Text.Json.JsonValueKind.String &&
                    decimal.TryParse(priceEl.GetString(), out var p) &&
                    decimal.TryParse(amountEl.GetString(), out var a))
                {
                    list.Add((p, a));
                }
                else
                {
                    // try numeric values
                    if ((priceEl.ValueKind == System.Text.Json.JsonValueKind.Number || priceEl.ValueKind == System.Text.Json.JsonValueKind.String) &&
                        (amountEl.ValueKind == System.Text.Json.JsonValueKind.Number || amountEl.ValueKind == System.Text.Json.JsonValueKind.String))
                    {
                        if (priceEl.TryGetDecimal(out var pn) && amountEl.TryGetDecimal(out var an))
                        {
                            list.Add((pn, an));
                        }
                    }
                }
            }
            return list;
        }
    }
}
