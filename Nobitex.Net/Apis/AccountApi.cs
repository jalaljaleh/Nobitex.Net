using System.Text.Json;
using System.Text.Json.Nodes;
using Nobitex.Net.Internal;
using Nobitex.Net.Models;
using Nobitex.Net.Serialization;

namespace Nobitex.Net.Apis;

/// <summary>
/// Account endpoints: login/token issuance, profile, notifications, limitations and API key management.
/// </summary>
public sealed class AccountApi
{
    private readonly NobitexClient _client;

    internal AccountApi(NobitexClient client) => _client = client;

    /// <summary>
    /// Logs in and returns a Nobitex token that can be used for authenticated requests.
    /// Endpoint: <c>POST /auth/login/</c>.
    /// </summary>
    /// <remarks>
    /// Nobitex recommends fetching the token directly from the dashboard. Use this method only if you
    /// fully understand the risks of storing a password in your code. When 2FA is enabled a TOTP code
    /// must be provided (header <c>X-TOTP</c>).
    /// </remarks>
    /// <param name="username">Account e-mail.</param>
    /// <param name="password">Account password.</param>
    /// <param name="totpCode">Current 2FA code (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<LoginResult> LoginAsync(
        string? username = null,
        string? password = null,
        string? totpCode = null,
        CancellationToken cancellationToken = default)
    {
        var user = username ?? _client.Options.Username ?? throw new ArgumentException("Username is required.");
        var pass = password ?? _client.Options.Password ?? throw new ArgumentException("Password is required.");

        if (!string.IsNullOrWhiteSpace(totpCode))
        {
            _client.Options.TotpCodeProvider = () => totpCode;
        }

        var node = await _client.SendNodeAsync(
            HttpMethod.Post, "/auth/login/", null,
            new Dictionary<string, object?>
            {
                ["username"] = user,
                ["password"] = pass,
                ["remember"] = _client.Options.RememberLogin ? "yes" : "no",
                ["device"] = _client.Options.Device,
                ["captcha"] = "api",
            },
            RateLimitGroups.Default, authenticated: false, cancellationToken).ConfigureAwait(false);

        var result = new LoginResult
        {
            Token = node["key"]?.GetValue<string>() ?? string.Empty,
            Device = node["device"]?.GetValue<string>(),
        };

        if (!string.IsNullOrEmpty(result.Token))
        {
            _client.SetApiToken(result.Token);
        }

        return result;
    }

    /// <summary>
    /// Returns the profile of the authenticated user.
    /// Endpoint: <c>GET /users/profile</c>.
    /// </summary>
    public Task<UserProfile> GetProfileAsync(CancellationToken cancellationToken = default)
        => _client.SendAsync<UserProfile>(
            HttpMethod.Get, "/users/profile", null, null, RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Returns the user preferences (language, currency, theme, ...).</summary>
    public Task<UserPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default)
        => _client.SendAsync<UserPreferences>(
            HttpMethod.Get, "/users/preferences", null, null, RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Returns the KYC / verification status of the account.</summary>
    public Task<VerificationStatus> GetVerificationStatusAsync(CancellationToken cancellationToken = default)
        => _client.SendAsync<VerificationStatus>(
            HttpMethod.Get, "/users/verification/status", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Returns the account limitations (withdrawal, trading, ...).</summary>
    public Task<AccountLimitations> GetLimitationsAsync(CancellationToken cancellationToken = default)
        => _client.SendAsync<AccountLimitations>(
            HttpMethod.Get, "/users/limitations", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Returns the notifications of the user.</summary>
    public async Task<IReadOnlyList<UserNotification>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/notifications/list", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["notifications"]?.Deserialize<List<UserNotification>>(NobitexJson.Options) ?? new List<UserNotification>();
    }

    /// <summary>
    /// Returns a short lived connection token required to subscribe to <c>private:</c> WebSocket channels.
    /// Endpoint: <c>GET /auth/ws/token</c>.
    /// </summary>
    public async Task<WebSocketToken> GetWebSocketTokenAsync(CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/auth/ws/token", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken).ConfigureAwait(false);

        return new WebSocketToken
        {
            Token = node["token"]?.GetValue<string>() ?? string.Empty,
        };
    }

    /// <summary>Lists the API keys of the user.</summary>
    public async Task<IReadOnlyList<ApiKey>> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/apikeys/list", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["keys"]?.Deserialize<List<ApiKey>>(NobitexJson.Options) ?? new List<ApiKey>();
    }

    /// <summary>
    /// Creates a new API key.
    /// Endpoint: <c>POST /apikeys/create</c>. Requires the <c>X-TOTP</c> header when 2FA is enabled.
    /// </summary>
    /// <remarks>
    /// The <b>private key is returned exactly once</b> — store it in a secret manager immediately.
    /// </remarks>
    public Task<CreatedApiKey> CreateApiKeyAsync(
        string name,
        ApiKeyPermissions permissions,
        string? description = null,
        IEnumerable<string>? ipAddressesWhitelist = null,
        DateTimeOffset? expirationDate = null,
        CancellationToken cancellationToken = default)
        => _client.SendAsync<CreatedApiKey>(
            HttpMethod.Post, "/apikeys/create", null,
            new Dictionary<string, object?>
            {
                ["name"] = name,
                ["description"] = description ?? string.Empty,
                ["permissions"] = permissions.ToApiString(),
                ["ipAddressesWhitelist"] = ipAddressesWhitelist?.ToList(),
                ["expirationDate"] = expirationDate?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            },
            RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Updates name, description or the IP whitelist of an API key.</summary>
    public Task<ApiKey> UpdateApiKeyAsync(
        string publicKey, string? name = null, string? description = null,
        IEnumerable<string>? ipAddressesWhitelist = null, CancellationToken cancellationToken = default)
        => _client.SendAsync<ApiKey>(
            HttpMethod.Post, "/apikeys/update/", null,
            new Dictionary<string, object?>
            {
                ["key"] = publicKey,
                ["name"] = name,
                ["description"] = description,
                ["ipAddressesWhitelist"] = ipAddressesWhitelist?.ToList(),
            },
            RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Deletes an API key.</summary>
    public Task<NobitexAcknowledge> DeleteApiKeyAsync(string publicKey, CancellationToken cancellationToken = default)
        => _client.SendAsync<NobitexAcknowledge>(
            HttpMethod.Post, $"/apikeys/delete/{Uri.EscapeDataString(publicKey)}/", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken);
}