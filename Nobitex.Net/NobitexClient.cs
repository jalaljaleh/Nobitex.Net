using Microsoft.Extensions.Logging;
using Nobitex.Net.Apis;
using Nobitex.Net.Authentication;
using Nobitex.Net.Internal;
using Nobitex.Net.Serialization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nobitex.Net;

/// <summary>
/// The main entry point of the library. Provides access to every Nobitex REST endpoint through
/// the <see cref="MarketData"/>, <see cref="Trading"/>, <see cref="Wallet"/>, <see cref="Account"/>
/// and <see cref="Margin"/> sub clients.
/// </summary>
/// <remarks>
/// <para>
/// The client is thread safe; a single instance can be shared across the whole application
/// (see the HttpClient guidance from Microsoft). Every public method is asynchronous, accepts a
/// <see cref="CancellationToken"/> and throws a derived <see cref="NobitexException"/> on failure.
/// </para>
/// <para>Example:</para>
/// <code>
/// var client = new NobitexClient(new NobitexClientOptions { ApiToken = "..." });
/// var stats = await client.MarketData.GetMarketStatsAsync("btc", "rls");
/// </code>
/// </remarks>
public sealed partial class NobitexClient : IDisposable, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ILogger _logger;
    private readonly RateLimiter _rateLimiter;
    private readonly Random _random = new();
    private string? _apiToken;
    private bool _disposed;

    /// <summary>Gets the resolved options of this client instance.</summary>
    public NobitexClientOptions Options { get; }

    /// <summary>Public market data endpoints (no authentication required).</summary>
    public MarketDataApi MarketData { get; }

    /// <summary>Spot trading endpoints: place, list, cancel orders and read own trades.</summary>
    public TradingApi Trading { get; }

    /// <summary>Wallet endpoints: balances, deposits, withdrawals and conversions.</summary>
    public WalletApi Wallet { get; }

    /// <summary>Account endpoints: login, profile, API keys and the WebSocket token.</summary>
    public AccountApi Account { get; }

    /// <summary>Margin (leverage) trading endpoints.</summary>
    public MarginApi Margin { get; }

    /// <summary>Creates a client with default options.</summary>
    public NobitexClient() : this(new NobitexClientOptions())
    {
    }

    /// <summary>Creates a client that owns its own <see cref="HttpClient"/>.</summary>
    /// <param name="options">Client configuration.</param>
    public NobitexClient(NobitexClientOptions options)
        : this(CreateHttpClient(options), options, ownsHttpClient: true)
    {
    }

    /// <summary>Creates a client from an externally managed <see cref="HttpClient"/> (IHttpClientFactory).</summary>
    /// <param name="httpClient">The HTTP client to use.</param>
    /// <param name="options">Client configuration.</param>
    public NobitexClient(HttpClient httpClient, NobitexClientOptions options)
        : this(httpClient, options, ownsHttpClient: false)
    {
    }

    private NobitexClient(HttpClient httpClient, NobitexClientOptions options, bool ownsHttpClient)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
        _logger = (options.LoggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
            .CreateLogger<NobitexClient>();
        _rateLimiter = new RateLimiter(options.EnableClientRateLimiting);
        _apiToken = string.IsNullOrWhiteSpace(options.ApiToken) ? null : options.ApiToken;

        _httpClient.BaseAddress ??= new Uri(options.BaseUrl);
        _httpClient.Timeout = options.Timeout;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        }
        if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        MarketData = new MarketDataApi(this);
        Trading = new TradingApi(this);
        Wallet = new WalletApi(this);
        Account = new AccountApi(this);
        Margin = new MarginApi(this);
    }

    /// <summary>Gets a value indicating whether the current instance is able to call private endpoints.</summary>
    public bool IsAuthenticated => _apiToken is not null || (Options.ApiKey?.IsValid ?? false);

    /// <summary>Overrides the API token used for authenticated requests (for example after a login).</summary>
    /// <param name="token">The token returned by <c>POST /auth/login/</c>.</param>
    public void SetApiToken(string token) => _apiToken = token;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------
    //  Transport
    // ---------------------------------------------------------------------

    /// <summary>
    /// Sends a request and deserialises the payload of the response into <typeparamref name="T"/>.
    /// The Nobitex envelope (<c>status</c>, <c>code</c>, <c>message</c>) is validated automatically.
    /// </summary>
    internal async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        string? query,
        object? body,
        string rateGroup,
        bool authenticated,
        CancellationToken cancellationToken = default)
    {
        var node = await SendNodeAsync(method, path, query, body, rateGroup, authenticated, cancellationToken)
            .ConfigureAwait(false);

        return node.Deserialize<T>(NobitexJson.Options)
            ?? throw new NobitexApiException("EmptyResponse", "The response could not be deserialised.", 200, node.ToJsonString());
    }

    /// <summary>
    /// Sends a request and returns the raw JSON payload. Useful for endpoints whose root also
    /// contains dynamic keys (for example <c>GET /v3/orderbook/all</c>).
    /// </summary>
    internal async Task<JsonNode> SendNodeAsync(
        HttpMethod method,
        string path,
        string? query,
        object? body,
        string rateGroup,
        bool authenticated,
        CancellationToken cancellationToken = default)
    {
        // Fail fast when a private endpoint is called without any configured credential.
        Options.Validate(authenticated);

        var relativeUrl = string.IsNullOrEmpty(query) ? path : $"{path}?{query}";
        var rawBody = body is null ? null : NobitexPayload.ToJson(body);
        var attempt = 0;

        while (true)
        {
            attempt++;
            await _rateLimiter.WaitAsync(rateGroup, cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(method, relativeUrl);
            ApplyAuthentication(request, method, relativeUrl, rawBody, authenticated);

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;

            try
            {
                _logger.LogDebug("Nobitex {Method} {Url} (attempt {Attempt})", method.Method, relativeUrl, attempt);
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt > Options.MaxRetries)
                {
                    throw new NobitexTransportException($"Request to {relativeUrl} failed.", ex);
                }

                await DelayForAttemptAsync(attempt, null, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using (response)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // ---- transport level status codes ------------------------------
                if ((int)response.StatusCode == (int)HttpStatusCode.TooManyRequests)
                {
                    var (backOff, limit) = ParseBackOff(content);
                    if (attempt <= Options.MaxRetries)
                    {
                        await Task.Delay(backOff, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new NobitexRateLimitException("Rate limit exceeded.", backOff, limit, content);
                }

                if ((int)response.StatusCode >= 500 && attempt <= Options.MaxRetries)
                {
                    await DelayForAttemptAsync(attempt, null, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if ((int)response.StatusCode == (int)HttpStatusCode.Unauthorized
                    || (int)response.StatusCode == (int)HttpStatusCode.Forbidden)
                {
                    throw new NobitexAuthenticationException(
                        $"Nobitex rejected the credentials ({(int)response.StatusCode}): {Truncate(content)}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new NobitexApiException("HttpError", $"Unexpected HTTP status {(int)response.StatusCode}.",
                        (int)response.StatusCode, content);
                }

                // ---- application level envelope --------------------------------
                JsonNode? node;
                try
                {
                    node = JsonNode.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
                }
                catch (Exception ex)
                {
                    throw new NobitexApiException("InvalidJson", "The response body is not valid JSON.", 200, content)
                    {
                        Source = ex.Source,
                    };
                }

                if (node is null)
                {
                    throw new NobitexApiException("InvalidJson", "Empty JSON response.", 200, content);
                }

                var status = node["status"]?.GetValue<string>();
                if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    var code = node["code"]?.GetValue<string>();
                    var message = node["message"]?.GetValue<string>();
                    var clientOrderId = node["clientOrderId"]?.GetValue<string>();

                    throw code is not null && IsOrderRejection(code)
                        ? new NobitexOrderRejectedException(code, message, clientOrderId, content)
                        : new NobitexApiException(code, message, 200, content);
                }

                return node;
            }
        }
    }

    /// <summary>Attaches the authentication headers for the current request.</summary>
    private void ApplyAuthentication(HttpRequestMessage request, HttpMethod method, string relativeUrl, string? body, bool authenticated)
    {
        if (!authenticated)
        {
            return;
        }

        var apiKey = Options.ApiKey;
        if (apiKey is not null && apiKey.IsValid)
        {
            // signature = base64( Ed25519( timestamp + METHOD + url + body ) )
            var signature = NobitexRequestSigner.Sign(apiKey, method.Method, relativeUrl, body);

            request.Headers.TryAddWithoutValidation("Nobitex-Key", signature.Key);
            request.Headers.TryAddWithoutValidation("Nobitex-Timestamp", signature.Timestamp);
            request.Headers.TryAddWithoutValidation("Nobitex-Signature", signature.Signature);
            return;
        }

        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            throw new NobitexAuthenticationException("This endpoint requires authentication but no token/api-key is configured.");
        }

        request.Headers.TryAddWithoutValidation("Authorization", $"Token {_apiToken}");

        var totp = Options.TotpCodeProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(totp))
        {
            request.Headers.TryAddWithoutValidation("X-TOTP", totp);
        }
    }

    private async Task DelayForAttemptAsync(int attempt, TimeSpan? fixedDelay, CancellationToken cancellationToken)
    {
        var delay = fixedDelay
            ?? TimeSpan.FromMilliseconds(Options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
        var jitter = TimeSpan.FromMilliseconds(_random.Next(0, 150));
        await Task.Delay(delay + jitter, cancellationToken).ConfigureAwait(false);
    }

    private static (TimeSpan backOff, int? limit) ParseBackOff(string content)
    {
        int? limit = null;
        var seconds = 1d;

        try
        {

            var node = JsonNode.Parse(content);

            if (node?["backOff"] is { } backOffNode)
            {
                seconds = backOffNode.GetValue<double>();
            }

            if (node?["limit"] is JsonValue limitNode && limitNode.TryGetValue<int>(out int limitValue))
            {
                limit = limitValue;
            }
        }
        catch
        {
            // Body was not JSON - fall back to the default delay.
        }

        return (TimeSpan.FromSeconds(seconds), limit);
    }

    private static bool IsOrderRejection(string code)
        => code is nameof(NobitexErrorCode.InvalidOrderPrice)
            or nameof(NobitexErrorCode.BadPrice)
            or nameof(NobitexErrorCode.PriceConditionFailed)
            or nameof(NobitexErrorCode.OverValueOrder)
            or nameof(NobitexErrorCode.SmallOrder)
            or nameof(NobitexErrorCode.DuplicateOrder)
            or nameof(NobitexErrorCode.InvalidMarketPair)
            or nameof(NobitexErrorCode.MarketClosed)
            or nameof(NobitexErrorCode.TradingUnavailable)
            or nameof(NobitexErrorCode.FeatureUnavailable)
            or nameof(NobitexErrorCode.DuplicateClientOrderId);

    private static string Truncate(string value) => value.Length <= 400 ? value : value[..400] + "…";

    private static HttpClient CreateHttpClient(NobitexClientOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            AutomaticDecompression = DecompressionMethods.All,
        };

        return new HttpClient(handler, disposeHandler: true);
    }
}