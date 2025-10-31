using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nobtiex.Websocket.Net
{
    using System.Text.Json.Serialization;

    public sealed class ConnectionTokenResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
