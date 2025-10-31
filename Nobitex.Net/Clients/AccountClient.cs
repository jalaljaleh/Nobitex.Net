using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class AccountClient : IAccountClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public AccountClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    public Task<NobitexProfileResponse?> GetProfileAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/users/profile"));
        return _transport.SendAsync<NobitexProfileResponse>(req, ct)!;
    }


    // WalletClient: POST /users/limitations
    /// <summary>
    /// Get current user limitations (withdraw/deposit/trade limits and enabled features).
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /users/limitations
    /// No request body required. Authorization token must be present in request headers.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="AccountLimitationsResponse"/> or null.</returns>
    public Task<AccountLimitationsResponse?> GetUserLimitationsAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/users/limitations"));
        return _transport.SendAsync<AccountLimitationsResponse>(req, ct);
    }

    /// <summary>
    /// Add a new bank account for the user.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /users/accounts-add
    /// Rate limit: 30 requests per 30 minutes
    /// Body: JSON { number, shaba, bank }
    /// </remarks>
    /// <param name="request">Bank account payload (number, shaba, bank).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="AccountAddResponse"/> or null.</returns>
    public Task<AccountAddResponse?> AddBankAccountAsync(AddBankAccountRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/users/accounts-add"))
        {
            Content = content
        };

        return _transport.SendAsync<AccountAddResponse>(req, ct);
    }



    /// <summary>
    /// Add a new bank card for the user.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /users/cards-add
    /// Rate limit: 30 requests per 30 minutes
    /// Body: JSON { number, bank }
    /// </remarks>
    /// <param name="request">Card payload (number, bank).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="CardAddResponse"/> or null.</returns>
    public Task<CardAddResponse?> AddBankCardAsync(AddBankCardRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/users/cards-add"))
        {
            Content = content
        };

        return _transport.SendAsync<CardAddResponse>(req, ct);
    }

    /// <summary>
    /// Generate a blockchain deposit address for a currency or wallet.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /users/wallets/generate-address
    /// Rate limit: 30 requests per hour
    /// Body:
    /// - currency (string) preferred; if provided replaces wallet
    /// - wallet (string/int) legacy; required only if currency is not provided
    /// - network (string) optional (e.g., "BSC")
    /// </remarks>
    /// <param name="request">Request containing currency or wallet and optional network.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="GenerateAddressResponse"/> or null.</returns>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    /// <exception cref="ArgumentException">If neither currency nor wallet is provided.</exception>
    public Task<GenerateAddressResponse?> GenerateAddressAsync(GenerateAddressRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // API requires either currency (preferred) or wallet (legacy)
        if (string.IsNullOrWhiteSpace(request.Currency) && string.IsNullOrWhiteSpace(request.Wallet))
            throw new ArgumentException("Either currency or wallet must be provided.", nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/users/wallets/generate-address"))
        {
            Content = content
        };

        return _transport.SendAsync<GenerateAddressResponse>(req, ct);
    }

    /// <summary>
    /// Get user's delegation limit for margin positions for a given currency.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /margin/delegation-limit
    /// Rate limit: 12 requests per minute
    /// Query:
    /// - currency (string) required: requested currency (src of the market), e.g., "btc"
    /// </remarks>
    /// <param name="currency">Currency code (required).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="DelegationLimitResponse"/> or null.</returns>
    /// <exception cref="ArgumentException">If currency is null/empty.</exception>
    public Task<DelegationLimitResponse?> GetMarginDelegationLimitAsync(string currency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("currency is required.", nameof(currency));

        var path = $"/margin/delegation-limit?currency={Uri.EscapeDataString(currency)}";
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<DelegationLimitResponse>(req, ct);
    }

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };


}
