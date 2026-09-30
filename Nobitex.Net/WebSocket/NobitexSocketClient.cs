using Nobitex.Net.Models;
using Nobitex.Net.Serialization;
using System.Text.Json;

namespace Nobitex.Net.WebSocket;

/// <summary>Order book update payload.</summary>
public sealed class OrderBookEventArgs : EventArgs
{
    /// <summary>Market symbol, e.g. <c>BTCIRT</c>.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Parsed order book snapshot / delta.</summary>
    public OrderBook OrderBook { get; init; } = new();
}

/// <summary>Public trade payload.</summary>
public sealed class PublicTradeEventArgs : EventArgs
{
    /// <summary>Market symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The trade.</summary>
    public PublicTrade Trade { get; init; } = new();
}

/// <summary>Market statistics payload.</summary>
public sealed class MarketStatsEventArgs : EventArgs
{
    /// <summary>Market symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Statistics.</summary>
    public MarketStats Stats { get; init; } = new();
}

/// <summary>Candle payload.</summary>
public sealed class CandleEventArgs : EventArgs
{
    /// <summary>Market symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Candle interval.</summary>
    public string Interval { get; init; } = string.Empty;

    /// <summary>The candle.</summary>
    public Candle Candle { get; init; } = new();
}

/// <summary>Private order update payload.</summary>
public sealed class PrivateOrderEventArgs : EventArgs
{
    /// <summary>The updated order.</summary>
    public Order Order { get; init; } = new();
}

/// <summary>Private trade payload.</summary>
public sealed class PrivateTradeEventArgs : EventArgs
{
    /// <summary>The executed trade.</summary>
    public MyTrade Trade { get; init; } = new();
}

/// <summary>
/// High level realtime client for the Nobitex WebSocket (Centrifugo).
/// It exposes strongly typed events for the public and private channels and keeps the
/// subscriptions alive across reconnects.
/// </summary>
public sealed class NobitexSocketClient : IAsyncDisposable
{
    private readonly NobitexClientOptions _options;
    private readonly NobitexClient? _restClient;
    private readonly CentrifugoClient _centrifugo;
    private readonly Dictionary<string, LocalOrderBook> _localBooks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a socket client. When <paramref name="restClient"/> is provided it is used to fetch the private channel token.</summary>
    public NobitexSocketClient(NobitexClientOptions options, NobitexClient? restClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _restClient = restClient;
        _centrifugo = new CentrifugoClient(options.WebSocketUrl, GetConnectionTokenAsync, options.LoggerFactory);

        _centrifugo.Connected += (_, e) => Connected?.Invoke(this, e);
        _centrifugo.Disconnected += (_, e) => Disconnected?.Invoke(this, e);
        _centrifugo.ErrorOccurred += (_, e) => ErrorOccurred?.Invoke(this, e);
        _centrifugo.Publication += OnPublication;
    }

    /// <summary>Raised when the socket connects (also after every reconnect).</summary>
    public event EventHandler<CentrifugoConnectionEventArgs>? Connected;

    /// <summary>Raised when the socket disconnects.</summary>
    public event EventHandler<CentrifugoConnectionEventArgs>? Disconnected;

    /// <summary>Raised when an internal error happens (the client keeps running).</summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>Raised on every order book update.</summary>
    public event EventHandler<OrderBookEventArgs>? OrderBookUpdated;

    /// <summary>Raised on every public trade.</summary>
    public event EventHandler<PublicTradeEventArgs>? PublicTrade;

    /// <summary>Raised on every market statistics update.</summary>
    public event EventHandler<MarketStatsEventArgs>? MarketStats;

    /// <summary>Raised on every candle update.</summary>
    public event EventHandler<CandleEventArgs>? CandleUpdated;

    /// <summary>Raised when one of the user's own orders changes.</summary>
    public event EventHandler<PrivateOrderEventArgs>? PrivateOrderUpdated;

    /// <summary>Raised when one of the user's own orders is filled.</summary>
    public event EventHandler<PrivateTradeEventArgs>? PrivateTrade;

    /// <summary>Raw access to every publication, whatever the channel.</summary>
    public event EventHandler<CentrifugoPublicationEventArgs>? RawPublication;

    /// <summary>Gets a value indicating whether the socket is connected.</summary>
    public bool IsConnected => _centrifugo.IsConnected;

    /// <summary>Gets the list of subscribed channels.</summary>
    public IReadOnlyCollection<string> Subscriptions => _centrifugo.Subscriptions;

    /// <summary>Opens the connection to <c>wss://ws.nobitex.ir/connection/websocket</c>.</summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
        => _centrifugo.ConnectAsync(cancellationToken);

    /// <summary>Closes the connection.</summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _centrifugo.DisconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Subscribes to the order book channel of a market.</summary>
    public Task SubscribeOrderBookAsync(string symbol, CancellationToken cancellationToken = default)
        => SubscribeAsync(NobitexChannels.OrderBook(symbol), "fossil", cancellationToken);

    /// <summary>Subscribes to the public trades channel of a market.</summary>
    public Task SubscribeTradesAsync(string symbol, CancellationToken cancellationToken = default)
        => SubscribeAsync(NobitexChannels.Trades(symbol), null, cancellationToken);

    /// <summary>Subscribes to the market statistics channel of a market.</summary>
    public Task SubscribeMarketStatsAsync(string symbol, CancellationToken cancellationToken = default)
        => SubscribeAsync(NobitexChannels.MarketStats(symbol), null, cancellationToken);

    /// <summary>Subscribes to the candle channel of a market.</summary>
    public Task SubscribeCandlesAsync(string symbol, string interval = "1m", CancellationToken cancellationToken = default)
        => SubscribeAsync(NobitexChannels.Candles(symbol) + $"-{interval}", null, cancellationToken);

    /// <summary>
    /// Subscribes to the private orders channel. Requires <see cref="NobitexClientOptions.ApiToken"/>
    /// (or an authenticated to fetch the connection token, and the
    /// per-user <c>websocket_auth_param</c> value.
    /// </summary>
    /// <param name="websocketAuthParam">
    /// The <c>{websocket_auth_param}</c> value from the Nobitex dashboard. When omitted,
    /// <see cref="NobitexClientOptions.WebSocketAuthParam"/> is used instead.
    /// </param>
    /// <param name="cancellationToken"></param>
    public Task SubscribePrivateOrdersAsync(string? websocketAuthParam = null, CancellationToken cancellationToken = default)
        => SubscribeAsync(NobitexChannels.PrivateOrders(ResolveAuthParam(websocketAuthParam)), null, cancellationToken);

    /// <summary>Subscribes to the private trades channel.</summary>
    public Task SubscribePrivateTradesAsync(string? websocketAuthParam = null, CancellationToken cancellationToken = default)
        => SubscribeAsync(NobitexChannels.PrivateTrades(ResolveAuthParam(websocketAuthParam)), null, cancellationToken);

    /// <summary>Subscribes to the private wallet (balance) channel.</summary>
    public Task SubscribePrivateWalletsAsync(string? websocketAuthParam = null, CancellationToken cancellationToken = default)
        => SubscribeAsync(NobitexChannels.PrivateWallets(ResolveAuthParam(websocketAuthParam)), null, cancellationToken);

    /// <summary>Returns the effective websocket_auth_param, falling back to the options value.</summary>
    private string ResolveAuthParam(string? websocketAuthParam)
    {
        var value = websocketAuthParam ?? _options.WebSocketAuthParam;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "The websocket_auth_param is required for private channels. " +
                "Pass it to the method or set NobitexClientOptions.WebSocketAuthParam.",
                nameof(websocketAuthParam));
        }

        return value;
    }

    /// <summary>Subscribes to any arbitrary channel.</summary>
    /// <param name="channel"></param>
    /// <param name="delta"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="onPublication">Optional callback invoked for every publication.</param>
    public async Task SubscribeAsync(
        string channel, string? delta = null, CancellationToken cancellationToken = default,
        Action<JsonElement>? onPublication = null)
    {
        if (onPublication is not null)
        {
            RawPublication += (_, e) =>
            {
                if (string.Equals(e.Channel, channel, StringComparison.Ordinal))
                {
                    onPublication(e.Data);
                }
            };
        }

        await _centrifugo.SubscribeAsync(channel, delta, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Unsubscribes from a channel.</summary>
    public Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        _localBooks.Remove(channel);
        return _centrifugo.UnsubscribeAsync(channel, cancellationToken);
    }

    /// <summary>
    /// Creates (or returns the existing) local order book that is automatically updated by the
    /// publications of the given symbol. Use <see cref="LocalOrderBook.Current"/> to read it.
    /// </summary>
    public LocalOrderBook CreateLocalOrderBook(string symbol)
    {
        var channel = NobitexChannels.OrderBook(symbol);

        if (!_localBooks.TryGetValue(symbol, out var book))
        {
            book = new LocalOrderBook(symbol);
            _localBooks[symbol] = book;
        }

        _ = SubscribeOrderBookAsync(symbol);

        return book;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _centrifugo.Publication -= OnPublication;
        await _centrifugo.DisposeAsync().ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------

    private async Task<string?> GetConnectionTokenAsync()
    {
        if (_restClient is not null)
        {
            var token = await _restClient.Account.GetWebSocketTokenAsync().ConfigureAwait(false);
            return token.Token;
        }

        return string.IsNullOrWhiteSpace(_options.ApiToken) ? null : _options.ApiToken;
    }

    private void OnPublication(object? sender, CentrifugoPublicationEventArgs e)
    {
        RawPublication?.Invoke(this, e);

        try
        {
            if (e.Channel.StartsWith("public:orderbook-", StringComparison.Ordinal))
            {
                var symbol = e.Channel["public:orderbook-".Length..];
                var book = e.Data.Deserialize<OrderBook>(NobitexJson.Options) ?? new OrderBook();
                book.Symbol = symbol;
                OrderBookUpdated?.Invoke(this, new OrderBookEventArgs { Symbol = symbol, OrderBook = book });

                if (_localBooks.TryGetValue(symbol, out var local))
                {
                    local.Apply(book);
                }
            }
            else if (e.Channel.StartsWith("public:trades-", StringComparison.Ordinal))
            {
                var symbol = e.Channel["public:trades-".Length..];
                var trade = e.Data.Deserialize<PublicTrade>(NobitexJson.Options);
                if (trade is not null)
                {
                    PublicTrade?.Invoke(this, new PublicTradeEventArgs { Symbol = symbol, Trade = trade });
                }
            }
            else if (e.Channel.StartsWith("public:stats-", StringComparison.Ordinal))
            {
                var symbol = e.Channel["public:stats-".Length..];
                var stats = e.Data.Deserialize<MarketStats>(NobitexJson.Options);
                if (stats is not null)
                {
                    MarketStats?.Invoke(this, new MarketStatsEventArgs { Symbol = symbol, Stats = stats });
                }
            }
            else if (e.Channel.StartsWith("public:candles-", StringComparison.Ordinal))
            {
                var parts = e.Channel["public:candles-".Length..].Split('-');
                var candle = e.Data.Deserialize<Candle>(NobitexJson.Options);
                if (candle is not null)
                {
                    CandleUpdated?.Invoke(this, new CandleEventArgs
                    {
                        Symbol = parts.Length > 0 ? parts[0] : string.Empty,
                        Interval = parts.Length > 1 ? string.Join('-', parts[1..]) : string.Empty,
                        Candle = candle,
                    });
                }
            }
            else if (e.Channel.StartsWith("private:orders", StringComparison.Ordinal))
            {
                var order = e.Data.Deserialize<Order>(NobitexJson.Options);
                if (order is not null)
                {
                    PrivateOrderUpdated?.Invoke(this, new PrivateOrderEventArgs { Order = order });
                }
            }
            else if (e.Channel.StartsWith("private:trades", StringComparison.Ordinal))
            {
                var trade = e.Data.Deserialize<MyTrade>(NobitexJson.Options);
                if (trade is not null)
                {
                    PrivateTrade?.Invoke(this, new PrivateTradeEventArgs { Trade = trade });
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }
}