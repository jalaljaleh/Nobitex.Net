namespace Nobitex.Websocket.Net.Abstractions
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal client surface used by consumers.
    /// Implementations handle connect/reconnect, subscribe/unsubscribe and event callbacks.
    /// </summary>
    public interface IWebsocketCentrifugoClient : IAsyncDisposable
    {
        /// <summary>
        /// Start background connect loop. Safe to call multiple times.
        /// </summary>
        void Start();

        /// <summary>
        /// Subscribe to a channel. Channel names must include websocketAuthParam for private channels.
        /// </summary>
        Task SubscribeAsync(string channel, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unsubscribe from a channel.
        /// </summary>
        Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish data to a channel (if allowed).
        /// </summary>
        Task PublishAsync(string channel, object data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fired for any raw incoming Centrifugo frame as JsonElement.
        /// </summary>
        event Func<JsonElement, Task>? OnFrame;

        /// <summary>
        /// Fired when a "message" frame arrives (params object passed).
        /// </summary>
        event Func<JsonElement, Task>? OnMessage;
    }
}
