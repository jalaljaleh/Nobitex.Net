namespace Nobitex.Websocket.Net
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;


    /// <summary>
    /// Parses incoming Centrifugo frames / params into typed domain objects (currently Orderbook).
    /// Usage: call TryParseOrderbook(frameJsonElement, out var orderbook) where frameJsonElement is the params object your client receives.
    /// Handles two common shapes:
    /// 1) { "asks": [...], "bids": [...], "lastTradePrice": "...", "lastUpdate": 1726... }    (direct)
    /// 2) { "push": { "channel": "public:orderbook-BTCIRT", "pub": { "data": "{\"asks\":...}", "offset": 123 } } }  (push wrapper)
    /// Also handles when params contains { "channel": "...", "data": {...} } shape from Centrifugo subscribe message.
    /// </summary>
    public sealed class MessageParser
    {
        private readonly ILogger<MessageParser>? _logger;

        public MessageParser(ILogger<MessageParser>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Try parse an Orderbook from a received JsonElement frame (params or full frame).
        /// Returns true if parsed; out orderbook is populated.
        /// </summary>
        public bool TryParseOrderbook(JsonElement frame, out Orderbook orderbook)
        {
            orderbook = new Orderbook();

            // 1) Direct snapshot: { asks: [...], bids: [...], lastTradePrice, lastUpdate }
            if (frame.ValueKind == JsonValueKind.Object &&
                frame.TryGetProperty("asks", out var asksEl) &&
                frame.TryGetProperty("bids", out var bidsEl))
            {
                return ParseDirectSnapshot(frame, out orderbook);
            }

            // 2) Push wrapper at root: { "push": { "channel": "...", "pub": { "data": "...", "offset": 123 } } }
            if (frame.ValueKind == JsonValueKind.Object && frame.TryGetProperty("push", out var pushEl))
            {
                // try pushEl.pub.data
                if (pushEl.TryGetProperty("pub", out var pubEl) && pubEl.ValueKind == JsonValueKind.Object)
                {
                    if (pubEl.TryGetProperty("data", out var dataEl))
                    {
                        // data may be stringified JSON
                        if (dataEl.ValueKind == JsonValueKind.String)
                        {
                            var inner = dataEl.GetString();
                            if (!string.IsNullOrWhiteSpace(inner))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(inner);
                                    return TryParseOrderbook(doc.RootElement, out orderbook);
                                }
                                catch { /* fallthrough */ }
                            }
                        }
                        else if (dataEl.ValueKind == JsonValueKind.Object)
                        {
                            return TryParseOrderbook(dataEl, out orderbook);
                        }
                    }
                }
            }

            // 3) Centrifugo method="message" style: { "method":"message","params": { "channel":"...","data":... } }
            if (frame.ValueKind == JsonValueKind.Object && frame.TryGetProperty("method", out var m) && m.GetString() == "message")
            {
                if (frame.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object)
                {
                    // params.data might be string or object, or params may contain push-like wrapper
                    if (p.TryGetProperty("data", out var dataField))
                    {
                        if (dataField.ValueKind == JsonValueKind.String)
                        {
                            var inner = dataField.GetString();
                            if (!string.IsNullOrWhiteSpace(inner))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(inner);
                                    return TryParseOrderbook(doc.RootElement, out orderbook);
                                }
                                catch { /* fallthrough */ }
                            }
                        }
                        else if (dataField.ValueKind == JsonValueKind.Object)
                        {
                            return TryParseOrderbook(dataField, out orderbook);
                        }
                    }

                    // sometimes params may embed pub or push
                    if (p.TryGetProperty("push", out var nestedPush)) return TryParseOrderbook(nestedPush, out orderbook);
                    if (p.TryGetProperty("pub", out var nestedPub)) return TryParseOrderbook(nestedPub, out orderbook);
                }
            }

            // 4) If nothing matched
            return false;

            // local helper
            bool ParseDirectSnapshot(JsonElement el, out Orderbook ob)
            {
                ob = new Orderbook();
                try
                {
                    var asks = el.GetProperty("asks");
                    var bids = el.GetProperty("bids");
                    ob = new Orderbook
                    {
                        Asks = Orderbook.ParseEntries(asks),
                        Bids = Orderbook.ParseEntries(bids),
                        LastTradePrice = el.TryGetProperty("lastTradePrice", out var ltp) && ltp.ValueKind == JsonValueKind.String && decimal.TryParse(ltp.GetString(), out var p) ? p : null,
                        LastUpdate = el.TryGetProperty("lastUpdate", out var lu) && lu.ValueKind == JsonValueKind.Number && lu.TryGetInt64(out var ms) ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : (DateTimeOffset?)null
                    };
                    return true;
                }
                catch
                {
                    ob = new Orderbook();
                    return false;
                }
            }
        }

    }
}
