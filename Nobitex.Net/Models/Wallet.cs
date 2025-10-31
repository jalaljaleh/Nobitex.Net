using System.Text.Json.Serialization;

namespace Nobitex.Net;
public record WalletAddress(string Currency, string Address, bool IsConfirmed);
public record WalletBalance(string Currency, decimal Available, decimal Locked);
public record WalletBalanceResponse(
    [property: JsonPropertyName("balance")] string Balance,
    [property: JsonPropertyName("status")] string Status
);


public record WalletTransactionsListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("transactions")] IReadOnlyList<WalletTransaction> Transactions,
    [property: JsonPropertyName("hasNext")] bool HasNext
);

public record WalletTransaction(
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("calculatedFee")] string CalculatedFee,
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("description")] string? Description
);


public record WalletTransactionsHistoryResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("transactions")] IReadOnlyList<WalletTransactionItem> Transactions,
    [property: JsonPropertyName("hasNext")] bool HasNext
);

public record WalletTransactionItem(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("balance")] string? Balance,
    [property: JsonPropertyName("tp")] string? TradeType,
    [property: JsonPropertyName("calculatedFee")] string? CalculatedFee,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("currency")] string Currency
);




public record WalletDepositsListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("deposits")] IReadOnlyList<WalletDepositItem> Deposits,
    [property: JsonPropertyName("hasNext")] bool HasNext
);

public record WalletDepositItem(
    [property: JsonPropertyName("txHash")] string TxHash,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("confirmed")] bool Confirmed,
    [property: JsonPropertyName("transaction")] WalletDepositTransaction? Transaction,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("blockchainUrl")] string? BlockchainUrl,
    [property: JsonPropertyName("confirmations")] int? Confirmations,
    [property: JsonPropertyName("requiredConfirmations")] int? RequiredConfirmations,
    [property: JsonPropertyName("amount")] string Amount
);

public record WalletDepositTransaction(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("calculatedFee")] string? CalculatedFee
);





/// <summary>
/// Response for GET /v2/wallets
/// {
///   "status": "ok",
///   "wallets": {
///     "RLS": { "id": 133777, "balance": "0E-10", "blocked": "0" },
///     "BTC": { "id": 133778, "balance": "0E-10", "blocked": "0" }
///   }
/// }
/// </summary>
public record WalletsV2Response(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("wallets")] IDictionary<string, WalletV2Item> Wallets
);

/// <summary>
/// Per-currency wallet info in /v2/wallets
/// </summary>
public record WalletV2Item(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("balance")] string Balance,
    [property: JsonPropertyName("blocked")] string Blocked
);







/// <summary>
/// Response for GET /users/wallets/list
/// </summary>
public record WalletsListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("wallets")] IReadOnlyList<WalletListItem> Wallets
);

/// <summary>
/// Reuse the wallet item shape used for wallets list responses.
/// Adjust fields nullable if some endpoints omit them.
/// </summary>
public record WalletListItem(
    [property: JsonPropertyName("depositAddress")] string? DepositAddress,
    [property: JsonPropertyName("depositTag")] string? DepositTag,
    [property: JsonPropertyName("depositInfo")] IDictionary<string, DepositInfoItem>? DepositInfo,
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("balance")] string? Balance,
    [property: JsonPropertyName("blockedBalance")] string? BlockedBalance,
    [property: JsonPropertyName("activeBalance")] string? ActiveBalance,
    [property: JsonPropertyName("rialBalance")] string? RialBalance,
    [property: JsonPropertyName("rialBalanceSell")] string? RialBalanceSell
);

/// <summary>
/// Per-network deposit info object (keyed by network name)
/// Example:
/// "depositInfo": {
///   "BTC": { "address": "...", "tag": null },
///   "BSC": { "address": null, "tag": null }
/// }
/// </summary>
public record DepositInfoItem(
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("tag")] string? Tag
);





/// <summary>
/// Request payload for POST /wallets/transfer
/// </summary>
public record WalletsTransferRequest(
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("src")] string Src,
    [property: JsonPropertyName("dst")] string Dst
);

/// <summary>
/// Response for POST /wallets/transfer
/// </summary>
public record WalletsTransferResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("srcWallet")] WalletListItem? SrcWallet,
    [property: JsonPropertyName("dstWallet")] WalletListItem? DstWallet,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);









public record WithdrawRequest(
    [property: JsonPropertyName("wallet")] long Wallet,
    [property: JsonPropertyName("network")] string? Network,
    [property: JsonPropertyName("invoice")] string? Invoice,
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("explanations")] string? Explanations,
    [property: JsonPropertyName("noTag")] bool? NoTag,
    [property: JsonPropertyName("tag")] string? Tag
);

public record WithdrawResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("withdraw")] WithdrawInfo? Withdraw,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);




/// <summary>
/// Withdrawal info (reuse for creation / confirm / list responses).
/// Monetary fields are strings to preserve formatting and precision.
/// </summary>
public record WithdrawInfo(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("network")] string? Network,
    [property: JsonPropertyName("invoice")] string? Invoice,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("tag")] string? Tag,
    [property: JsonPropertyName("wallet_id")] long? WalletId,
    [property: JsonPropertyName("blockchain_url")] string? BlockchainUrl,
    [property: JsonPropertyName("is_cancelable")] bool? IsCancelable
);






// Request / response DTOs
public record WithdrawConfirmRequest(
        [property: JsonPropertyName("withdraw")] long Withdraw,
        [property: JsonPropertyName("otp")] int? Otp
    );

public record WithdrawConfirmResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("withdraw")] WithdrawInfo? Withdraw,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);





    /// <summary>
    /// Response for GET /users/wallets/withdraws/list
    /// </summary>
    public record WithdrawsListResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("withdraws")] IReadOnlyList<WithdrawInfo>? Withdraws,
        [property: JsonPropertyName("hasNext")] bool? HasNext
    );







/// <summary>
/// Response for GET /withdraws/{id}
/// Reuses WithdrawInfo for the withdraw object.
/// </summary>
public record WithdrawGetResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("withdraw")] WithdrawInfo? Withdraw,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message
);

