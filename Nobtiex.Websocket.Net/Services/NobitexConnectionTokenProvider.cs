namespace Nobitex.Websocket.Net.Services
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Nobitex.Websocket.Net.Abstractions;

    /// <summary>
    /// Fetches connection token from https://apiv2.nobitex.ir/auth/ws/token/
    /// Requires an API token (Bearer-style header "Authorization: Token {ApiToken}").
    /// Throws UnauthorizedAccessException on 403.
    /// </summary>
    public sealed class NobitexConnectionTokenProvider : IConnectionTokenProvider
    {
        private readonly HttpClient _http;
        private readonly string _apiToken;
        private readonly ILogger<NobitexConnectionTokenProvider>? _logger;

        public NobitexConnectionTokenProvider(HttpClient httpClient, string apiToken, ILogger<NobitexConnectionTokenProvider>? logger = null)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiToken = apiToken ?? throw new ArgumentNullException(nameof(apiToken));
            _logger = logger;
        }

        public async Task<string> GetConnectionTokenAsync(CancellationToken cancellationToken = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "auth/ws/token/");
            req.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiToken);
            req.Headers.Accept.ParseAdd("application/json");

            using var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);

            if (res.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger?.LogWarning("Token endpoint returned 403 Forbidden.");
                throw new UnauthorizedAccessException("API token unauthorized for WS token endpoint (403).");
            }

            res.EnsureSuccessStatusCode();

            var content = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("token", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String)
                {
                    return tokenEl.GetString()!;
                }
            }
            catch (JsonException je)
            {
                _logger?.LogError(je, "Failed to parse token response.");
                throw new InvalidOperationException("Invalid JSON from token endpoint.", je);
            }

            throw new InvalidOperationException("Token property missing in token endpoint response.");
        }
    }
}
