namespace Nobitex.Websocket.Net
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Nobitex.Websocket.Net.Abstractions;


    public sealed class WebsocketCentrifugoClient : IWebsocketCentrifugoClient
    {
        private readonly Uri _uri;
        private readonly IConnectionTokenProvider _tokenProvider;
        private readonly ILogger<WebsocketCentrifugoClient>? _logger;
        private readonly Channel<string> _sendChannel = Channel.CreateUnbounded<string>();
        private readonly ConcurrentDictionary<string, byte> _subscriptions = new();
        private readonly ConcurrentDictionary<long, string> _pendingSubscribeById = new();
        private readonly SemaphoreSlim _startLock = new(1, 1);

        private ClientWebSocket? _ws;
        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;
        private volatile bool _connected;

        public event Func<JsonElement, Task>? OnFrame;
        public event Func<JsonElement, Task>? OnMessage;
        public event Func<string, Task>? OnOutgoingFrame;
        public WebsocketCentrifugoClient(IConnectionTokenProvider tokenProvider, NobitexWebsocketOptions options, ILogger<WebsocketCentrifugoClient>? logger = null)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _logger = logger;
            _uri = options?.WebsocketUrl ?? throw new ArgumentNullException(nameof(options));
        }

        public void Start()
        {
            if (_loopTask != null) return;
            _loopCts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunAsync(_loopCts.Token));
        }

        public async ValueTask DisposeAsync()
        {
            _loopCts?.Cancel();
            try { if (_loopTask != null) await _loopTask.ConfigureAwait(false); } catch { }
            try { _ws?.Dispose(); } catch { }
            _startLock.Dispose();
            _loopCts?.Dispose();
        }

        /// <summary>
        /// Register interest in channel. This will track requested subscription and will send subscribe frame
        /// immediately if connected, otherwise it will be sent automatically after connect ack.
        /// </summary>
        public async Task SubscribeAsync(string channel, CancellationToken cancellationToken = default)
        {
            _subscriptions[channel] = 0;
            var id = NewId();
            _pendingSubscribeById[id] = channel;

            if (_connected && _ws != null && _ws.State == WebSocketState.Open)
            {
                // send subscribe now
                await SendFrameAsync(new { subscribe = new { channel }, id }, cancellationToken).ConfigureAwait(false);
                _logger?.LogInformation("Sent subscribe for {channel} (id={id})", channel, id);
            }
            else
            {
                _logger?.LogInformation("Queued subscribe for {channel} (will send after connect ack)", channel);
            }
        }

        public async Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
        {
            _subscriptions.TryRemove(channel, out _);
            var id = NewId();
            if (_connected && _ws != null && _ws.State == WebSocketState.Open)
            {
                _pendingSubscribeById[id] = channel;
                await SendFrameAsync(new { unsubscribe = new { channel }, id }, cancellationToken).ConfigureAwait(false);
                _logger?.LogInformation("Sent unsubscribe for {channel} (id={id})", channel, id);
            }
            else
            {
                _logger?.LogInformation("Unsubscribe queued/removed for {channel}", channel);
            }
        }

        public async Task PublishAsync(string channel, object data, CancellationToken cancellationToken = default)
        {
            await SendFrameAsync(new { publish = new { channel, data }, id = NewId() }, cancellationToken).ConfigureAwait(false);
        }

        private long NewId() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ (long)Environment.TickCount;

        private async Task SendFrameAsync(object frame, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(frame, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            if (OnOutgoingFrame != null) _ = OnOutgoingFrame(json);
            await _sendChannel.Writer.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        }


        private async Task RunAsync(CancellationToken token)
        {
            var rnd = new Random();
            var backoffMs = 1000;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _startLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        await ConnectAndRunLoopsAsync(token).ConfigureAwait(false);
                        backoffMs = 1000;
                    }
                    finally
                    {
                        _startLock.Release();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (UnauthorizedAccessException uae)
                {
                    _logger?.LogError(uae, "Unauthorized fetching WS token. Stopping reconnect attempts.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Websocket main loop error; will retry with backoff.");
                }

                var jitter = rnd.Next(0, 500);
                try
                {
                    await Task.Delay(backoffMs + jitter, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                backoffMs = Math.Min(backoffMs * 2, 30_000);
                try { _ws?.Dispose(); } catch { }
                _ws = null;
                _connected = false;
            }
        }

        private async Task ConnectAndRunLoopsAsync(CancellationToken token)
        {
            var connectionToken = await _tokenProvider.GetConnectionTokenAsync(token).ConfigureAwait(false);

            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_uri, token).ConfigureAwait(false);
            _logger?.LogInformation("Connected to {uri}", _uri);

            // send connect frame with token
            var connectFrame = new { connect = new { token = connectionToken }, id = 1L };
            await SendRawAsync(JsonSerializer.Serialize(connectFrame), token).ConfigureAwait(false);
            _logger?.LogInformation("Sent connect frame (id=1)");

            var receiveTask = ReceiveLoopAsync(token);
            var sendTask = SendLoopAsync(token);

            var completed = await Task.WhenAny(receiveTask, sendTask).ConfigureAwait(false);

            if (completed.IsFaulted)
            {
                await completed.ConfigureAwait(false);
            }

            _logger?.LogInformation("WebSocket loops ended, closing socket.");
            try
            {
                if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch { }
        }

        private async Task SendLoopAsync(CancellationToken token)
        {
            if (_ws == null) return;
            while (!token.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                string json;
                try
                {
                    json = await _sendChannel.Reader.ReadAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                await SendRawAsync(json, token).ConfigureAwait(false);
            }
        }

        private async Task SendRawAsync(string json, CancellationToken token)
        {
            if (OnOutgoingFrame != null) _ = OnOutgoingFrame(json);
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            var seg = new ArraySegment<byte>(bytes);
            await _ws.SendAsync(seg, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }


        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            if (_ws == null) return;
            var buffer = new byte[64 * 1024];
            while (!token.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult? res;
                do
                {
                    res = await _ws.ReceiveAsync(buffer, token).ConfigureAwait(false);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        _logger?.LogInformation("WebSocket close received: {code}", res.CloseStatus);
                        try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None).ConfigureAwait(false); } catch { }
                        return;
                    }
                    ms.Write(buffer, 0, res.Count);
                } while (!res.EndOfMessage);

                var text = Encoding.UTF8.GetString(ms.ToArray());
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement.Clone();

                    if (OnFrame != null) _ = OnFrame(root);

                    // handle result frames (connect ack and subscribe/unsubscribe results)
                    if (root.TryGetProperty("result", out var resultEl) && root.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var idVal))
                    {
                        // connect ack (id==1)
                        if (idVal == 1)
                        {
                            _connected = true;
                            _logger?.LogInformation("Connect acknowledged (id=1). Re-sending queued subscriptions: {count}", _subscriptions.Count);

                            // send all tracked subscriptions now
                            foreach (var channel in _subscriptions.Keys)
                            {
                                var sid = NewId();
                                _pendingSubscribeById[sid] = channel;
                                await SendFrameAsync(new { subscribe = new { channel }, id = sid }, token).ConfigureAwait(false);
                                _logger?.LogInformation("Sent subscribe for {channel} (id={id})", channel, sid);
                            }
                        }
                        else
                        {
                            // if this id corresponds to a pending subscribe/unsubscribe, log it and remove mapping
                            if (_pendingSubscribeById.TryRemove(idVal, out var ch))
                            {
                                _logger?.LogInformation("Received result for id={id} (channel={channel})", idVal, ch);
                            }
                            else
                            {
                                _logger?.LogDebug("Received result for id={id} without mapping", idVal);
                            }
                        }
                    }

                    // errors with ids
                    if (root.TryGetProperty("error", out var errEl) && root.TryGetProperty("id", out var errIdEl) && errIdEl.TryGetInt64(out var errId))
                    {
                        if (_pendingSubscribeById.TryRemove(errId, out var ch))
                        {
                            _logger?.LogWarning("Subscribe/Unsubscribe (id={id}, channel={channel}) returned error: {err}", errId, ch, errEl.ToString());
                        }
                        else
                        {
                            _logger?.LogWarning("Received error frame with id {id}: {err}", errId, errEl.ToString());
                        }
                    }

                    // message frames
                    if (root.TryGetProperty("method", out var method) && method.GetString() == "message")
                    {
                        if (root.TryGetProperty("params", out var p))
                        {
                            if (OnMessage != null) _ = OnMessage(p);
                        }
                    }

                    // Centrifugo-style push wrapper: root may contain "push"
                    if (root.TryGetProperty("push", out var pushEl))
                    {
                        // for convenience, deliver pushEl to OnMessage as well
                        if (OnMessage != null) _ = OnMessage(pushEl);
                    }

                    // any error (without id)
                    if (root.TryGetProperty("error", out var err))
                    {
                        _logger?.LogWarning("Received error frame: {err}", err.ToString());
                    }
                }
                catch (JsonException je)
                {
                    _logger?.LogWarning(je, "Invalid JSON frame received");
                }
            }

            // connection closed
            _connected = false;
        }
    }
}
