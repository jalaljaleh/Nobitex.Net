using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class WalletClient : IWalletClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public WalletClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    /// <summary>
    /// Get the balance for a specific currency wallet.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /users/wallets/balance?currency={currency}
    /// Returns current available balance for the requested currency as returned by the API.
    /// </remarks>
    /// <param name="currency">Currency code (e.g. "btc", "rls"). This value will be URL-encoded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WalletBalanceResponse"/> or null if the transport returns no content.</returns>
    public Task<WalletBalanceResponse?> GetBalanceAsync(string currency, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), $"/users/wallets/balance?currency={Uri.EscapeDataString(currency)}"));
        return _transport.SendAsync<WalletBalanceResponse>(req, ct);
    }

    /// <summary>
    /// Get the latest transactions for a specific wallet (paginated).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /users/wallets/transactions/list
    /// Query parameters:
    /// - wallet (int): wallet identifier.
    /// - page (int): page number (default 1).
    /// - pageSize (int): items per page (default 50).
    /// </remarks>
    /// <param name="walletId">Wallet identifier to query.</param>
    /// <param name="page">Page number, 1-based.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WalletTransactionsListResponse"/> containing the page of transactions.</returns>
    public Task<WalletTransactionsListResponse?> GetLatestWalletTransactionsAsync(int walletId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), $"/users/wallets/transactions/list?wallet={walletId}&page={page}&pageSize={pageSize}"));
        return _transport.SendAsync<WalletTransactionsListResponse>(req, ct);
    }

    /// <summary>
    /// Get transactions history for a wallet or currency (paginated).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /users/transactions-history
    /// Note: the API uses the query key "wallet" for the wallet identifier or currency string depending on the endpoint usage.
    /// Query parameters:
    /// - wallet (string): wallet id or currency identifier used by the API.
    /// - page (int): page number (default 1).
    /// - pageSize (int): items per page (default 50).
    /// </remarks>
    /// <param name="currency">Wallet id or currency identifier as required by the API (string).</param>
    /// <param name="page">Page number, 1-based.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WalletTransactionsHistoryResponse"/> containing the transactions page.</returns>
    public Task<WalletTransactionsHistoryResponse?> GetWalletTransactionsHistoryAsync(string currency, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), $"/users/transactions-history?wallet={currency}&page={page}&pageSize={pageSize}"));
        return _transport.SendAsync<WalletTransactionsHistoryResponse>(req, ct);
    }

    /// <summary>
    /// Get deposits for a wallet with optional date filters (paginated).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /users/wallets/deposits/list
    /// Query parameters:
    /// - wallet (int or "all"): wallet identifier, or "all" to fetch across wallets. Default when walletId == 0 is "all".
    /// - page (int): page number (default 1).
    /// - pageSize (int): items per page (default 50).
    /// - from (date, optional): start date filter in yyyy-MM-dd format.
    /// - to (date, optional): end date filter in yyyy-MM-dd format.
    /// Dates are only appended to the query if provided.
    /// </remarks>
    /// <param name="walletId">Wallet identifier. Use 0 to request "all" wallets.</param>
    /// <param name="page">Page number, 1-based.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="from">Optional start date filter (date portion only).</param>
    /// <param name="to">Optional end date filter (date portion only).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WalletDepositsListResponse"/> with deposit items and paging info.</returns>
    public Task<WalletDepositsListResponse?> GetWalletDepositsAsync(int walletId = 0, int page = 1, int pageSize = 50, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var q = new List<string> {
        $"wallet={(walletId == 0 ? "all": Uri.EscapeDataString(walletId.ToString()))}",
        $"page={Uri.EscapeDataString(page.ToString())}",
        $"pageSize={Uri.EscapeDataString(pageSize.ToString())}"  };

        if (from.HasValue)
            q.Add($"from={Uri.EscapeDataString(from.Value.ToString("yyyy-MM-dd"))}");
        if (to.HasValue)
            q.Add($"to={Uri.EscapeDataString(to.Value.ToString("yyyy-MM-dd"))}");

        var path = "/users/wallets/deposits/list?" + string.Join("&", q);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<WalletDepositsListResponse>(req, ct);
    }


    /// <summary>
    /// GET /v2/wallets
    /// Optional: currencies (comma separated, e.g. "rls,btc")
    /// Optional: type ("spot" or "margin", default "spot")
    /// Rate limit: 15 requests per minute
    /// </summary>
    public Task<WalletsV2Response?> GetWalletsV2Async(string? currencies = null, string type = "spot", CancellationToken ct = default)
    {
        var q = new List<string>();

        if (!string.IsNullOrWhiteSpace(currencies))
            q.Add($"currencies={Uri.EscapeDataString(currencies)}");

        if (!string.IsNullOrWhiteSpace(type))
            q.Add($"type={Uri.EscapeDataString(type)}");

        var path = "/v2/wallets" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<WalletsV2Response>(req, ct);
    }

    // WalletClient: GET /users/wallets/list
    /// <summary>
    /// Get user's wallets list (detailed).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /users/wallets/list
    /// Rate limit: 15 requests/minute (same class as other wallet endpoints)
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WalletsListResponse"/> or null.</returns>
    public Task<WalletsListResponse?> GetWalletsListAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/users/wallets/list"));
        return _transport.SendAsync<WalletsListResponse>(req, ct);
    }

    /// <summary>
    /// Transfer funds between spot and margin wallets.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /wallets/transfer
    /// Rate limit: 10 requests per minute
    /// Body:
    /// - currency (string) required: currency code (e.g., "rls" or "usdt")
    /// - amount (string) required: monetary amount (use invariant culture or string to preserve precision)
    /// - src (string) required: "spot" or "margin"
    /// - dst (string) required: "spot" or "margin"
    /// src and dst must not be equal.
    /// </remarks>
    /// <param name="request">Transfer request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WalletsTransferResponse"/> or null.</returns>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    /// <exception cref="ArgumentException">If src == dst or required fields are missing.</exception>
    public Task<WalletsTransferResponse?> TransferToMarginAsync(WalletsTransferRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Currency)) throw new ArgumentException("Currency is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Amount)) throw new ArgumentException("Amount is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Src)) throw new ArgumentException("Src is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Dst)) throw new ArgumentException("Dst is required.", nameof(request));
        if (string.Equals(request.Src, request.Dst, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Src and Dst must be different.", nameof(request));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/wallets/transfer"))
        {
            Content = content
        };

        return _transport.SendAsync<WalletsTransferResponse>(req, ct);
    }

    /// <summary>
    /// Create a withdrawal request from a user wallet.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /users/wallets/withdraw
    /// Rate limit: 10 requests per 3 minutes
    /// If the destination address is not in the user's address book the server requires X-TOTP header (one-time code).
    /// For BTCLN invoice is required (invoice contains amount & address and supersedes amount/address fields).
    /// </remarks>
    /// <param name="request">Withdraw request payload.</param>
    /// <param name="totp">Optional TOTP code to send in X-TOTP header when required by the server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WithdrawResponse"/>.</returns>
    public Task<WithdrawResponse?> CreateWithdrawAsync(WithdrawRequest request, string? totp = null, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.Wallet <= 0) throw new ArgumentException("wallet is required and must be positive.", nameof(request.Wallet));

        // If invoice is not provided, amount is required for non-invoice flows
        if (string.IsNullOrWhiteSpace(request.Invoice) && string.IsNullOrWhiteSpace(request.Amount))
            throw new ArgumentException("Either invoice or amount must be provided.", nameof(request.Amount));

        // If noTag is false and network requires tag, then tag must be supplied (best-effort validation not exhaustive)
        var networksRequiringTag = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BNB", "EOS", "PMN", "XLM", "XRP" };
        if (networksRequiringTag.Contains(request.Network ?? string.Empty) && request.NoTag != true && string.IsNullOrWhiteSpace(request.Tag))
            throw new ArgumentException("Tag is required for the selected network unless noTag is true.", nameof(request.Tag));

        // Build request
        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/users/wallets/withdraw"))
        {
            Content = content
        };

        if (!string.IsNullOrWhiteSpace(totp))
            req.Headers.Add("X-TOTP", totp);

        return _transport.SendAsync<WithdrawResponse>(req, ct);
    }

    /// <summary>
    /// Confirm a previously created withdrawal request with an OTP.
    /// </summary>
    /// <remarks>
    /// Endpoint: POST /users/wallets/withdraw-confirm
    /// Rate limit: 30 requests per hour
    /// Use this when the withdrawal was created and the server sent an OTP (email/SMS).
    /// For safe/whitelisted targets an OTP may not be required.
    /// </remarks>
    /// <param name="request">Withdraw confirmation payload (withdraw id and otp when required).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WithdrawConfirmResponse"/>.</returns>
    /// <exception cref="ArgumentNullException">If request is null.</exception>
    /// <exception cref="ArgumentException">If withdraw id is not positive or otp is invalid when required.</exception>
    public Task<WithdrawConfirmResponse?> ConfirmWithdrawAsync(WithdrawConfirmRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.Withdraw <= 0) throw new ArgumentException("withdraw id is required and must be positive.", nameof(request.Withdraw));
        if (request.Otp.HasValue && (request.Otp < 0 || request.Otp > 9999999)) // len check is best-effort
            throw new ArgumentException("otp seems invalid.", nameof(request.Otp));

        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opts.BaseUrl), "/users/wallets/withdraw-confirm"))
        {
            Content = content
        };

        return _transport.SendAsync<WithdrawConfirmResponse>(req, ct);
    }


    /// <summary>
    /// Get the user's withdrawal requests (paginated).
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /users/wallets/withdraws/list
    /// Rate limit: 10 requests per 3 minutes (follow server-side limits)
    /// Default page size: 30 (client-side default)
    /// Query parameters:
    /// - page (int) optional, 1-based
    /// - pageSize (int) optional
    /// - status (string) optional (e.g., New, Verified, Done, Canceled)
    /// - fromId (long) optional (alternative to page/pageSize)
    /// </remarks>
    public Task<WithdrawsListResponse?> GetWithdrawsListAsync(
        int page = 1,
        int pageSize = 30,
        string? status = null,
        long? fromId = null,
        CancellationToken ct = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 30;

        var q = new List<string>();
        if (fromId.HasValue)
        {
            q.Add($"fromId={Uri.EscapeDataString(fromId.Value.ToString())}");
        }
        else
        {
            q.Add($"page={Uri.EscapeDataString(page.ToString())}");
            q.Add($"pageSize={Uri.EscapeDataString(pageSize.ToString())}");
        }

        if (!string.IsNullOrWhiteSpace(status)) q.Add($"status={Uri.EscapeDataString(status)}");

        var path = "/users/wallets/withdraws/list" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<WithdrawsListResponse>(req, ct);
    }

    /// <summary>
    /// Get a single withdrawal request by id.
    /// </summary>
    /// <remarks>
    /// Endpoint: GET /withdraws/{withdrawId}
    /// Rate limit: 60 requests per 2 minutes
    /// </remarks>
    /// <param name="withdrawId">Withdrawal identifier (required, positive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialized <see cref="WithdrawGetResponse"/> or null.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If withdrawId is not positive.</exception>
    public Task<WithdrawGetResponse?> GetWithdrawAsync(long withdrawId, CancellationToken ct = default)
    {
        if (withdrawId <= 0) throw new ArgumentOutOfRangeException(nameof(withdrawId), "withdrawId must be positive.");

        var path = $"/withdraws/{withdrawId}";
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), path));
        return _transport.SendAsync<WithdrawGetResponse>(req, ct);
    }



    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
