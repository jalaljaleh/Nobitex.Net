namespace Nobitex.Websocket.Net.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a connection token to be used in the Centrifugo connect frame.
    /// Implementations are responsible for fetching token from Nobitex REST endpoint using an API token.
    /// </summary>
    public interface IConnectionTokenProvider
    {
        /// <summary>
        /// Get a connection token for WebSocket connect.
        /// Throws UnauthorizedAccessException if API token is not authorized (maps to HTTP 403).
        /// </summary>
        Task<string> GetConnectionTokenAsync(CancellationToken cancellationToken = default);
    }
}
