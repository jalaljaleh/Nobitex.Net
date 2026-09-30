using Microsoft.Extensions.Logging;
using Nobitex.Net.Authentication;
using System.ComponentModel;

namespace Nobitex.Net;

/// <summary>
/// Configuration options for <see cref="NobitexClient"/>.
/// Mirrors the Microsoft.Extensions.Options pattern so the client can be registered
/// with <c>services.AddOptions&lt;NobitexClientOptions&gt;().BindConfiguration("Nobitex")</c>.
/// </summary>
public sealed class NobitexClientOptions
{
    /// <summary>Default Nobitex REST API base address.</summary>
    public const string ProductionApiUrl = "https://apiv2.nobitex.ir";

    /// <summary>Nobitex testnet (sandbox) REST API base address.</summary>
    public const string TestnetApiUrl = "https://testnetapiv2.nobitex.ir";

    /// <summary>Nobitex production WebSocket endpoint (Centrifugo).</summary>
    public const string ProductionWebSocketUrl = "wss://ws.nobitex.ir/connection/websocket";

    private string _baseUrl = ProductionApiUrl;
    private string _webSocketUrl = ProductionWebSocketUrl;
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private string _userAgent = "TraderBot/Nobitex.Net";
    private int _maxRetries = 3;
    private TimeSpan _retryBaseDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Base address of the REST API. Defaults to <see cref="ProductionApiUrl"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not an absolute URI.</exception>
    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            {
                throw new ArgumentException("BaseUrl must be an absolute URI.", nameof(value));
            }

            _baseUrl = value.TrimEnd('/');
        }
    }

    /// <summary>WebSocket endpoint.</summary>
    public string WebSocketUrl
    {
        get => _webSocketUrl;
        set => _webSocketUrl = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Long-lived API token obtained from the Nobitex dashboard (Settings → API Token).
    /// Sent as <c>Authorization: Token {value}</c>. Optional when an <see cref="ApiKey"/> is used.
    /// </summary>
    public string? ApiToken { get; set; }

    /// <summary>
    /// API-key credentials (public key + Ed25519 private key). When set, requests are authenticated
    /// with the three headers <c>Nobitex-Key</c>, <c>Nobitex-Signature</c> and <c>Nobitex-Timestamp</c>.
    /// </summary>
    public NobitexApiKeyCredentials? ApiKey { get; set; }

    /// <summary>
    /// The per-user <c>websocket_auth_param</c> value used to build private WebSocket channels
    /// (<c>private:orders#{websocket_auth_param}</c>). Optional; it can also be passed directly to the
    /// <c>SubscribePrivate…Async</c> methods of <see cref="Nobitex.Net.WebSocket.NobitexSocketClient"/>.
    /// </summary>
    public string? WebSocketAuthParam { get; set; }

    /// <summary>
    /// Provider for the current TOTP (2FA) code. Nobitex requires the <c>X-TOTP</c> header
    /// on some endpoints such as <c>POST /auth/login/</c> and <c>POST /apikeys/create</c>.
    /// </summary>
    public Func<string?>? TotpCodeProvider { get; set; }

    /// <summary>Username (e-mail)</summary>
    public string? Username { get; set; }

    /// <summary>Password used by.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// When <c>true</c>, <c>remember=yes</c> is sent on login so the returned token is valid for 30 days.
    /// </summary>
    public bool RememberLogin { get; set; } = true;

    /// <summary>Device identifier returned by a previous login; prevents the 1-hour withdrawal limitation.</summary>
    public string? Device { get; set; }

    /// <summary>
    /// Value of the <c>User-Agent</c> header. Nobitex strongly recommends the
    /// <c>TraderBot/XXXXX</c> pattern so support can identify automated clients.
    /// </summary>
    public string UserAgent
    {
        get => _userAgent;
        set => _userAgent = string.IsNullOrWhiteSpace(value) ? _userAgent : value.Trim();
    }

    /// <summary>Per-request timeout. The default is 30 seconds.</summary>
    [DefaultValue("00:00:30")]
    public TimeSpan Timeout
    {
        get => _timeout;
        set => _timeout = value <= TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    }

    /// <summary>
    /// Maximum number of automatic retries for transient failures (HTTP 5xx, network errors)
    /// and rate-limited requests (HTTP 429). Default is 3.
    /// </summary>
    public int MaxRetries
    {
        get => _maxRetries;
        set => _maxRetries = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    }

    /// <summary>Base delay used by the exponential backoff strategy. Default is 500 ms.</summary>
    public TimeSpan RetryBaseDelay
    {
        get => _retryBaseDelay;
        set => _retryBaseDelay = value <= TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    }

    /// <summary>
    /// Enables a client-side token-bucket rate limiter so the documented limits
    /// (for example 300 order requests / 10 minutes) are not exceeded by accident.
    /// </summary>
    public bool EnableClientRateLimiting { get; set; } = true;

    /// <summary>Enables HTTP response compression.</summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>Optional logger factory used for diagnostics.</summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>Creates a fresh instance of the options.</summary>
    public NobitexClientOptions()
    {
    }

    /// <summary>Creates options configured for the Nobitex production environment.</summary>
    /// <param name="apiToken">API token from the Nobitex dashboard.</param>
    /// <returns>A new <see cref="NobitexClientOptions"/> instance.</returns>
    public static NobitexClientOptions ForProduction(string? apiToken = null)
        => new() { BaseUrl = ProductionApiUrl, WebSocketUrl = ProductionWebSocketUrl, ApiToken = apiToken };

    /// <summary>Creates options configured for the Nobitex testnet (sandbox) environment.</summary>
    /// <param name="apiToken">Testnet API token.</param>
    /// <returns>A new <see cref="NobitexClientOptions"/> instance.</returns>
    public static NobitexClientOptions ForTestnet(string? apiToken = null)
        => new() { BaseUrl = TestnetApiUrl, ApiToken = apiToken };

    /// <summary>Validates the options and throws when mandatory credentials are missing.</summary>
    /// <param name="requireAuth">When <c>true</c>, at least one credential source must be present.</param>
    internal void Validate(bool requireAuth)
    {
        if (!requireAuth)
        {
            return;
        }

        var hasToken = !string.IsNullOrWhiteSpace(ApiToken);
        var hasKey = ApiKey is not null && ApiKey.IsValid;

        if (!hasToken && !hasKey)
        {
            throw new NobitexAuthenticationException(
                "No credentials configured. Set either ApiToken or ApiKey in NobitexClientOptions.");
        }
    }
}