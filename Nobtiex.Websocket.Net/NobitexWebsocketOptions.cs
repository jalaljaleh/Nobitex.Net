using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nobitex.Websocket.Net
{

    /// <summary>
    /// Configuration options for Nobitex websocket integration.
    /// </summary>
    public sealed class NobitexWebsocketOptions
    {
        /// <summary>
        /// Required. API token used to call the REST endpoint that issues a connection token.
        /// Set this from configuration or environment securely.
        /// </summary>
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Base address for the Nobitex API token endpoint. Defaults to the public apiv2 host.
        /// </summary>
        public Uri ApiBaseAddress { get; set; } = new Uri("https://apiv2.nobitex.ir/");

        /// <summary>
        /// WebSocket endpoint. You normally don't need to change this.
        /// </summary>
        public Uri WebsocketUrl { get; set; } = new Uri("wss://ws.nobitex.ir/connection/websocket");

        /// <summary>
        /// Optional user-specific websocketAuthParam used to subscribe to private channels,
        /// e.g. the string after '#'. Not required for public channels.
        /// </summary>
        public string? WebsocketAuthParam { get; set; }
    }
}
