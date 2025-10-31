namespace Nobitex.Websocket.Net
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Nobitex.Websocket.Net.Abstractions;

    public sealed class OrderbookSubscriptionHelper : IAsyncDisposable
    {
        private readonly IWebsocketCentrifugoClient _client;
        private readonly MessageParser _parser;
        private readonly ILogger<OrderbookSubscriptionHelper>? _logger;

        public event Func<Orderbook, Task>? OnOrderbook;

        public OrderbookSubscriptionHelper(IWebsocketCentrifugoClient client, MessageParser parser, ILogger<OrderbookSubscriptionHelper>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _logger = logger;
            _client.OnMessage += HandleMessage;
            _client.OnFrame += HandleFrame; // keep frame-level subscription to catch push wrappers
        }

        public async Task StartListening(string channel, CancellationToken token = default)
        {
            await _client.SubscribeAsync(channel, token).ConfigureAwait(false);
        }

        public async Task StopListening(string channel, CancellationToken token = default)
        {
            await _client.UnsubscribeAsync(channel, token).ConfigureAwait(false);
        }

        private Task HandleFrame(JsonElement frame)
        {
            // Try parse orderbook directly from the frame
            if (_parser.TryParseOrderbook(frame, out var ob))
            {
                // Extract market if present in push wrapper (push.channel)
                if (frame.TryGetProperty("push", out var push) && push.TryGetProperty("channel", out var ch))
                {
                    var market = ExtractMarketFromChannel(ch.GetString());
                    if (!string.IsNullOrEmpty(market))
                    {
                        ob = WithMarket(ob, market);
                    }
                }

                _ = Raise(ob);
            }
            return Task.CompletedTask;
        }

        private Task HandleMessage(JsonElement p)
        {
            // Typical Centrifugo params style often contains channel + data or push.pub
            // If params contains channel, extract market
            string? channel = null;
            if (p.TryGetProperty("channel", out var chEl))
            {
                channel = chEl.GetString();
            }
            else if (p.TryGetProperty("push", out var pushEl) && pushEl.ValueKind == JsonValueKind.Object && pushEl.TryGetProperty("channel", out var ch2))
            {
                channel = ch2.GetString();
            }

            if (_parser.TryParseOrderbook(p, out var ob))
            {
                if (!string.IsNullOrEmpty(channel))
                {
                    var market = ExtractMarketFromChannel(channel);
                    if (!string.IsNullOrEmpty(market))
                    {
                        ob = WithMarket(ob, market);
                    }
                }

                _ = Raise(ob);
            }
            return Task.CompletedTask;
        }

        private static Orderbook WithMarket(Orderbook src, string market)
        {
            return new Orderbook
            {
                Asks = src.Asks,
                Bids = src.Bids,
                LastTradePrice = src.LastTradePrice,
                LastUpdate = src.LastUpdate,
                Market = market
            };
        }

        private static string? ExtractMarketFromChannel(string? channel)
        {
            if (string.IsNullOrEmpty(channel)) return null;
            // Expect patterns like "public:orderbook-BTCIRT" -> extract after last '-' if pattern matches
            var parts = channel.Split(':', 2);
            if (parts.Length < 2) return null;
            var after = parts[1];
            if (after.StartsWith("orderbook-", StringComparison.OrdinalIgnoreCase))
            {
                return after.Substring("orderbook-".Length);
            }
            // fallback: try after '#'
            var idx = channel.IndexOf('#');
            if (idx >= 0 && idx + 1 < channel.Length) return channel.Substring(idx + 1);
            return null;
        }

        private async Task Raise(Orderbook ob)
        {
            try
            {
                if (OnOrderbook != null) await OnOrderbook(ob).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OnOrderbook handler failed");
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _client.OnMessage -= HandleMessage;
                _client.OnFrame -= HandleFrame;
            }
            catch { }
            await Task.CompletedTask;
        }
    }
}
