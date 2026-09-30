using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nobitex.Net.WebSocket;

/// <summary>Event data of a connection state change.</summary>
public sealed class CentrifugoConnectionEventArgs : EventArgs
{
    /// <summary>Client id assigned by the server (only on connect).</summary>
    public string? ClientId { get; init; }

    /// <summary>Disconnect reason (only on disconnect).</summary>
    public string? Reason { get; init; }

    /// <summary>Disconnect code (only on disconnect).</summary>
    public int Code { get; init; }
}

/// <summary>Event data of an incoming publication.</summary>
public sealed class CentrifugoPublicationEventArgs : EventArgs
{
    /// <summary>Channel the publication belongs to.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Payload of the publication.</summary>
    public JsonElement Data { get; init; }
}

/// <summary>
/// A dependency-free implementation of the Centrifugo WebSocket protocol used by Nobitex.
/// Handles connect (with optional JWT token), ping/pong, subscribe/unsubscribe, publications and
/// automatic reconnection with resubscription.
/// </summary>
public sealed class CentrifugoClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Uri _endpoint;
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<string, string> _subscriptions = new();
    private readonly Func<Task<string?>>? _tokenProvider;
    private readonly ILogger _logger;
    private int _nextId = 1;

    /// <summary>Raised when the connection is established (or re-established).</summary>
    public event EventHandler<CentrifugoConnectionEventArgs>? Connected;

    /// <summary>Raised when the connection is lost.</summary>
    public event EventHandler<CentrifugoConnectionEventArgs>? Disconnected;

    /// <summary>Raised for every publication received on a subscribed channel.</summary>
    public event EventHandler<CentrifugoPublicationEventArgs>? Publication;

    /// <summary>Raised for every channel that the server confirmed as subscribed.</summary>
    public event EventHandler<string>? Subscribed;

    /// <summary>Raised for every internal error. The client keeps running.</summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>Gets a value indicating whether the client is currently connected.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Gets the channels currently subscribed.</summary>
    public IReadOnlyCollection<string> Subscriptions => _subscriptions.Keys.ToList();

    /// <summary>Maximum delay between reconnection attempts.</summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Creates a Centrifugo client.</summary>
    /// <param name="endpoint">WebSocket endpoint, e.g. <c>wss://ws.nobitex.ir/connection/websocket</c>.</param>
    /// <param name="tokenProvider">
    /// Optional provider of the connection token. It is called on every (re)connect so short lived
    /// JWT tokens can be refreshed automatically.
    /// </param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public CentrifugoClient(string endpoint, Func<Task<string?>>? tokenProvider = null, ILoggerFactory? loggerFactory = null)
    {
        _endpoint = new Uri(endpoint ?? throw new ArgumentNullException(nameof(endpoint)));
        _tokenProvider = tokenProvider;
        _logger = (loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
            .CreateLogger<CentrifugoClient>();
    }

    /// <summary>
    /// Opens the connection and starts the receive loop. The method returns as soon as the
    /// server accepts the <c>connect</c> handshake.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _socket.ConnectAsync(_endpoint, cancellationToken).ConfigureAwait(false);

        string? token = _tokenProvider is null ? null : await SafeGetTokenAsync(cancellationToken).ConfigureAwait(false);

        var connect = new JsonObject
        {
            ["connect"] = token is null ? new JsonObject() : new JsonObject { ["token"] = token },
            ["id"] = _nextId++,
        };

        await SendRawAsync(connect, cancellationToken).ConfigureAwait(false);
        _ = Task.Run(() => ReceiveLoopAsync(_stopping.Token), _stopping.Token);
    }

    /// <summary>Subscribes to a channel. The subscription survives reconnections.</summary>
    public async Task SubscribeAsync(string channel, string? delta = null, CancellationToken cancellationToken = default)
    {
        _subscriptions[channel] = delta ?? string.Empty;

        if (!IsConnected)
        {
            return;
        }

        var parameters = new JsonObject { ["channel"] = channel };
        if (!string.IsNullOrEmpty(delta))
        {
            parameters["delta"] = delta;
        }

        await SendRawAsync(new JsonObject { ["subscribe"] = parameters, ["id"] = _nextId++ }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Unsubscribes from a channel and stops receiving its publications.</summary>
    public async Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        _subscriptions.Remove(channel);

        if (!IsConnected)
        {
            return;
        }

        await SendRawAsync(new JsonObject
        {
            ["unsubscribe"] = new JsonObject { ["channel"] = channel },
            ["id"] = _nextId++,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes the connection gracefully.</summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client shutdown", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Close failed");
        }
        finally
        {
            _stopping.Cancel();
            IsConnected = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _socket.Dispose();
        _sendLock.Dispose();
        _stopping.Dispose();
    }

    // ---------------------------------------------------------------------

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var message = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                message.Clear();

                while (_socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                        .ConfigureAwait(false);

                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        break;
                    }
                }

                if (message.Length == 0)
                {
                    continue;
                }

                await HandleMessageAsync(message.ToString(), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsConnected = false;
                Disconnected?.Invoke(this, new CentrifugoConnectionEventArgs { Reason = "connection lost" });
                await ReconnectAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task HandleMessageAsync(
        string json,
        CancellationToken cancellationToken)
    {
        JsonNode? node;

        try
        {
            node = JsonNode.Parse(json);
        }
        catch
        {
            _logger.LogWarning(
                "Received a non-JSON frame: {Frame}",
                json[..Math.Min(json.Length, 120)]);

            return;
        }

        if (node is null)
        {
            return;
        }

        // Centrifugo sends an empty object "{}" as a ping;
        // the pong must also be "{}".
        if (node is JsonObject root && root.Count == 0)
        {
            await SendRawAsync(
                new JsonObject(),
                cancellationToken).ConfigureAwait(false);

            return;
        }

        if (node["connect"] is JsonObject connect)
        {
            IsConnected = true;

            Connected?.Invoke(
                this,
                new CentrifugoConnectionEventArgs
                {
                    ClientId = connect["client"]?.GetValue<string>(),
                });

            foreach (var channel in _subscriptions.Keys.ToList())
            {
                await SubscribeAsync(
                    channel,
                    _subscriptions[channel],
                    cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (node["disconnect"] is JsonObject disconnect)
        {
            IsConnected = false;

            Disconnected?.Invoke(
                this,
                new CentrifugoConnectionEventArgs
                {
                    Code = disconnect["code"]?.GetValue<int>() ?? 0,
                    Reason = disconnect["reason"]?.GetValue<string>(),
                });

            return;
        }

        if (node["subscribe"] is JsonObject subscribed)
        {
            Subscribed?.Invoke(
                this,
                subscribed["channel"]?.GetValue<string>() ?? string.Empty);

            return;
        }

        if (node["error"] is JsonObject error)
        {
            ErrorOccurred?.Invoke(
                this,
                new InvalidOperationException(
                    error["message"]?.GetValue<string>() ?? "socket error"));

            return;
        }

        if (node["push"] is JsonObject push)
        {
            var channel =
                push["channel"]?.GetValue<string>()
                ?? string.Empty;

            var data = push["pub"]?["data"];

            if (data is not null)
            {
                using var document =
                    JsonDocument.Parse(data.ToJsonString());

                Publication?.Invoke(
                    this,
                    new CentrifugoPublicationEventArgs
                    {
                        Channel = channel,
                        Data = document.RootElement.Clone(),
                    });
            }
        }
    }
    private async Task ReconnectAsync()
    {
        var delay = TimeSpan.FromSeconds(1);

        while (!_stopping.IsCancellationRequested)
        {
            _logger.LogWarning("Reconnecting to {Endpoint} in {Seconds}s", _endpoint, delay.TotalSeconds);
            await Task.Delay(delay, _stopping.Token).ConfigureAwait(false);

            try
            {
                await ConnectAsync(_stopping.Token).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, MaxReconnectDelay.TotalMilliseconds));
            }
        }
    }

    private async Task SendRawAsync(JsonObject message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString(JsonOptions));

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<string?> SafeGetTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _tokenProvider!().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            return null;
        }
    }
}

//internal static class JsonNodeExtensions
//{
//    /// <summary>Converts a <see cref="JsonNode"/> into a read-only <see cref="JsonElement"/>.</summary>
//    public static JsonElement ToJsonDocumentRoot(this JsonNode node)
//        => JsonDocument.Parse(node.ToJsonString(JsonOptions)).RootElement.Clone();
//}