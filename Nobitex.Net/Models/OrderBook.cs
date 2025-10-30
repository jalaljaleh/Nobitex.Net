using System.Collections.Generic;

namespace Nobitex.Net;
public record OrderBookEntry(decimal Price, decimal Volume);

public record OrderBook(
    string Status,
    long LastUpdate,
    string? LastTradePrice,
    IReadOnlyList<OrderBookEntry> Asks,
    IReadOnlyList<OrderBookEntry> Bids
);
