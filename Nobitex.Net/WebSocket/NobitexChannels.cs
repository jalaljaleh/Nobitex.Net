using Nobitex.Net.Internal;

namespace Nobitex.Net.WebSocket;

/// <summary>
/// Builds the names of the Nobitex WebSocket channels.
/// </summary>
/// <remarks>
/// <para>
/// Nobitex uses <b>Centrifugo</b> at <c>wss://ws.nobitex.ir/connection/websocket</c>.
/// Channels are prefixed with <c>public:</c> or <c>private:</c>; private channels require a
/// connection token obtained from <c>GET /auth/ws/token</c> and use the
/// <c>private:channel#{websocket_auth_param}</c> naming convention.
/// </para>
/// <para>Limitations: 100 simultaneous connections per IP, 450 subscriptions per connection.</para>
/// </remarks>
public static class NobitexChannels
{
    /// <summary>Public order book channel, e.g. <c>public:orderbook-BTCIRT</c>.</summary>
    public static string OrderBook(string symbol) => $"public:orderbook-{NobitexSymbol.Normalize(symbol)}";

    /// <summary>Public trades channel, e.g. <c>public:trades-BTCIRT</c>.</summary>
    public static string Trades(string symbol) => $"public:trades-{NobitexSymbol.Normalize(symbol)}";

    /// <summary>Public candle channel, e.g. <c>public:candles-BTCIRT</c>.</summary>
    public static string Candles(string symbol) => $"public:candles-{NobitexSymbol.Normalize(symbol)}";

    /// <summary>Public market statistics channel.</summary>
    public static string MarketStats(string symbol) => $"public:stats-{NobitexSymbol.Normalize(symbol)}";

    /// <summary>Private channel that streams updates of the user's own orders.</summary>
    /// <param name="websocketAuthParam">Per user unique parameter shown in the Nobitex dashboard.</param>
    public static string PrivateOrders(string websocketAuthParam) => $"private:orders#{websocketAuthParam}";

    /// <summary>Private channel that streams the user's own trades.</summary>
    public static string PrivateTrades(string websocketAuthParam) => $"private:trades#{websocketAuthParam}";

    /// <summary>Private channel that streams wallet balance updates.</summary>
    public static string PrivateWallets(string websocketAuthParam) => $"private:wallets#{websocketAuthParam}";

    /// <summary>Returns <c>true</c> when the channel requires authentication.</summary>
    public static bool IsPrivate(string channel) => channel.StartsWith("private:", StringComparison.Ordinal);
}