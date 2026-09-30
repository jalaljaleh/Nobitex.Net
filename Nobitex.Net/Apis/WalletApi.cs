using System.Text.Json;
using System.Text.Json.Nodes;
using Nobitex.Net.Internal;
using Nobitex.Net.Models;
using Nobitex.Net.Serialization;

namespace Nobitex.Net.Apis;

/// <summary>
/// Wallet endpoints: balances, deposit addresses, withdrawals and instant conversion.
/// Withdrawal endpoints require the <c>WITHDRAW</c> permission of the API key.
/// </summary>
public sealed class WalletApi
{
    private readonly NobitexClient _client;

    internal WalletApi(NobitexClient client) => _client = client;

    /// <summary>
    /// Returns the available balance of a single wallet.
    /// Endpoint: <c>POST /users/wallets/balance</c>.
    /// </summary>
    /// <param name="currency">Wallet code, e.g. <c>btc</c>, <c>rls</c>, <c>usdt</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<decimal> GetBalanceAsync(string currency, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Post, "/users/wallets/balance", null,
            new Dictionary<string, object?> { ["currency"] = currency.ToLowerInvariant() },
            RateLimitGroups.Wallet, authenticated: true, cancellationToken).ConfigureAwait(false);

        return decimal.Parse(
            node["balance"]?.GetValue<string>() ?? "0",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns every wallet of the user with balances.
    /// Endpoint: <c>GET /users/wallets/list</c>.
    /// </summary>
    public async Task<IReadOnlyList<Wallet>> GetWalletsAsync(CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/users/wallets/list", null, null,
            RateLimitGroups.Wallet, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["wallets"]?.Deserialize<List<Wallet>>(NobitexJson.Options) ?? new List<Wallet>();
    }

    /// <summary>
    /// Returns the wallets of the requested currency codes (v2 endpoint).
    /// Endpoint: <c>GET /v2/wallets?codes=btc,usdt</c>.
    /// </summary>
    public async Task<IReadOnlyList<Wallet>> GetWalletsV2Async(
        IEnumerable<string>? codes = null, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/v2/wallets",
            QueryBuilder.Build(new { codes = codes is null ? null : string.Join(',', codes) }),
            null, RateLimitGroups.Wallet, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["wallets"]?.Deserialize<List<Wallet>>(NobitexJson.Options) ?? new List<Wallet>();
    }

    /// <summary>
    /// Returns the deposit address of a wallet on a specific network.
    /// Endpoint: <c>POST /users/wallets/deposit</c>.
    /// </summary>
    /// <param name="currency">Wallet code, e.g. <c>usdt</c>.</param>
    /// <param name="network">Network code, e.g. <c>TRX</c> / <c>BSC</c>. Optional.</param>
    /// <param name="cancellationToken"></param>
    public Task<DepositAddress> GetDepositAddressAsync(
        string currency, string? network = null, CancellationToken cancellationToken = default)
        => _client.SendAsync<DepositAddress>(
            HttpMethod.Post, "/users/wallets/deposit", null,
            new Dictionary<string, object?> { ["code"] = currency.ToLowerInvariant(), ["network"] = network },
            RateLimitGroups.Wallet, authenticated: true, cancellationToken);

    /// <summary>
    /// Requests a withdrawal. Endpoint: <c>POST /users/wallets/withdraw</c>.
    /// </summary>
    /// <remarks>Requires the <c>WITHDRAW</c> permission; Nobitex recommends an IP whitelist for such keys.</remarks>
    public Task<Withdrawal> WithdrawAsync(WithdrawalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _client.SendAsync<Withdrawal>(
            HttpMethod.Post, "/users/wallets/withdraw", null,
            new Dictionary<string, object?>
            {
                ["currency"] = request.Currency.ToLowerInvariant(),
                ["amount"] = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["address"] = request.Address,
                ["network"] = request.Network,
                ["description"] = request.Description,
                ["fee"] = request.Fee?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["calcFee"] = request.CalculateFee ? "yes" : null,
                ["withFee"] = request.IncludeFee ? "yes" : null,
                ["actType"] = "api",
            },
            RateLimitGroups.Wallet, authenticated: true, cancellationToken);
    }

    /// <summary>Confirms a pending withdrawal (when e-mail confirmation is enabled).</summary>
    public Task<NobitexAcknowledge> ConfirmWithdrawAsync(long withdrawId, CancellationToken cancellationToken = default)
        => _client.SendAsync<NobitexAcknowledge>(
            HttpMethod.Post, "/users/wallets/withdraw-confirm", null,
            new Dictionary<string, object?> { ["withdrawId"] = withdrawId },
            RateLimitGroups.Wallet, authenticated: true, cancellationToken);

    /// <summary>Cancels a pending withdrawal.</summary>
    public Task<NobitexAcknowledge> CancelWithdrawAsync(long withdrawId, CancellationToken cancellationToken = default)
        => _client.SendAsync<NobitexAcknowledge>(
            HttpMethod.Post, "/users/wallets/withdraw-cancel", null,
            new Dictionary<string, object?> { ["withdrawId"] = withdrawId },
            RateLimitGroups.Wallet, authenticated: true, cancellationToken);

    /// <summary>Returns a single withdrawal with its current status.</summary>
    public Task<Withdrawal> GetWithdrawAsync(long withdrawId, CancellationToken cancellationToken = default)
        => _client.SendAsync<Withdrawal>(
            HttpMethod.Get, $"/withdraws/{withdrawId}", null, null,
            RateLimitGroups.Wallet, authenticated: true, cancellationToken);

    /// <summary>Lists the user withdrawals (paginated).</summary>
    public async Task<IReadOnlyList<Withdrawal>> GetWithdrawsAsync(
        string? currency = null, string? from = null, string? to = null,
        int? page = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/users/wallets/withdraws/list",
            QueryBuilder.Build(new { currency, from, to, page, pageSize }),
            null, RateLimitGroups.Wallet, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["withdraws"]?.Deserialize<List<Withdrawal>>(NobitexJson.Options) ?? new List<Withdrawal>();
    }

    /// <summary>Lists the user deposits (paginated).</summary>
    public async Task<IReadOnlyList<Deposit>> GetDepositsAsync(
        string? currency = null, string? from = null, string? to = null,
        int? page = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/users/wallets/deposits/list",
            QueryBuilder.Build(new { currency, from, to, page, pageSize }),
            null, RateLimitGroups.Wallet, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["deposits"]?.Deserialize<List<Deposit>>(NobitexJson.Options) ?? new List<Deposit>();
    }

    /// <summary>Lists the transactions of a wallet.</summary>
    /// <param name="walletId">Id of the wallet (see <see cref="GetWalletsAsync"/>).</param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="cancellationToken"></param>
    public async Task<IReadOnlyList<WalletTransaction>> GetTransactionsAsync(
        long walletId, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/users/wallets/transactions/list",
            QueryBuilder.Build(new { wallet = walletId, order = "-createdAt", page, pageSize }),
            null, RateLimitGroups.Wallet, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["transactions"]?.Deserialize<List<WalletTransaction>>(NobitexJson.Options) ?? new List<WalletTransaction>();
    }

    /// <summary>
    /// Instantly converts one currency into another inside the wallet (no order book).
    /// Endpoint: <c>POST /users/wallets/convert</c>. Requires the <c>TRADE</c> permission.
    /// </summary>
    public Task<ConversionResult> ConvertAsync(
        string convertFrom, string convertTo, decimal amount, CancellationToken cancellationToken = default)
        => _client.SendAsync<ConversionResult>(
            HttpMethod.Post, "/users/wallets/convert", null,
            new Dictionary<string, object?>
            {
                ["convertFrom"] = convertFrom.ToLowerInvariant(),
                ["convertTo"] = convertTo.ToLowerInvariant(),
                ["amount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            RateLimitGroups.PlaceOrder, authenticated: true, cancellationToken);
}