using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nobtiex.Websocket.Net.Utilities
{
    using System;
    using System.Text;
    using System.Text.Json;

    public static class JwtHelper
    {
        public static DateTimeOffset? ParseExpiry(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt)) return null;
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            try
            {
                var payload = parts[1];
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
                var json = Encoding.UTF8.GetString(bytes);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(exp);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static bool ShouldRefresh(DateTimeOffset? expiry, TimeSpan margin)
        {
            if (expiry == null) return true;
            return expiry.Value <= DateTimeOffset.UtcNow.Add(margin);
        }
    }
}
