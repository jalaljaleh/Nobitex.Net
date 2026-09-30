using System.Text.Json.Serialization;

namespace Nobitex.Net.Models;

/// <summary>A user wallet with its balances.</summary>
public sealed class Wallet
{
    /// <summary>Wallet id, used by the transactions endpoint.</summary>
    public long Id { get; set; }

    /// <summary>Currency code, e.g. <c>btc</c>, <c>rls</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Total balance.</summary>
    public decimal Balance { get; set; }

    /// <summary>Balance locked by open orders.</summary>
    [JsonPropertyName("blockedBalance")]
    public decimal BlockedBalance { get; set; }

    /// <summary>Available (not blocked) balance.</summary>
    [JsonPropertyName("activeBalance")]
    public decimal ActiveBalance { get; set; }

    /// <summary>Value of the wallet in IRR.</summary>
    [JsonPropertyName("rialBalance")]
    public decimal RialBalance { get; set; }

    /// <summary>Value of the wallet in USDT.</summary>
    [JsonPropertyName("usdtBalance")]
    public decimal UsdtBalance { get; set; }
}

/// <summary>Deposit address of a wallet on a network.</summary>
public sealed class DepositAddress
{
    /// <summary>Wallet currency code.</summary>
    public string? Code { get; set; }

    /// <summary>Network, e.g. <c>TRX</c>, <c>BSC</c>, <c>ERC20</c>.</summary>
    public string? Network { get; set; }

    /// <summary>Deposit address.</summary>
    public string? Address { get; set; }

    /// <summary>Memo / destination tag when the network requires one.</summary>
    public string? Tag { get; set; }

    /// <summary>Indicates whether a memo/tag must be provided with every deposit.</summary>
    public bool IsTagRequired { get; set; }
}

/// <summary>Request body of <c>POST /users/wallets/withdraw</c>.</summary>
public sealed class WithdrawalRequest
{
    /// <summary>Currency to withdraw, e.g. <c>usdt</c>.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Amount to withdraw (in the currency units, fee excluded).</summary>
    public decimal Amount { get; set; }

    /// <summary>Destination address.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Network, e.g. <c>TRX</c>. Mandatory for multi-chain assets.</summary>
    public string? Network { get; set; }

    /// <summary>Optional description shown in the history.</summary>
    public string? Description { get; set; }

    /// <summary>Explicit network fee. Use <see cref="CalculateFee"/> to let Nobitex compute it.</summary>
    public decimal? Fee { get; set; }

    /// <summary>When <c>true</c> Nobitex calculates the fee automatically.</summary>
    public bool CalculateFee { get; set; } = true;

    /// <summary>When <c>true</c> the fee is deducted from the amount instead of the balance.</summary>
    public bool IncludeFee { get; set; }

    /// <summary>Validates the request locally.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new ArgumentException("Currency is required.", nameof(Currency));
        }

        if (Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            throw new ArgumentException("Address is required.", nameof(Address));
        }
    }
}

/// <summary>A withdrawal as returned by Nobitex.</summary>
public sealed class Withdrawal
{
    /// <summary>Withdrawal id.</summary>
    public long Id { get; set; }

    /// <summary>Currency.</summary>
    public string? Currency { get; set; }

    /// <summary>Withdrawn amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Network fee.</summary>
    public decimal Fee { get; set; }

    /// <summary>Destination address.</summary>
    public string? Address { get; set; }

    /// <summary>Network.</summary>
    public string? Network { get; set; }

    /// <summary>Status, e.g. <c>pending</c>, <c>confirmed</c>, <c>accepted</c>, <c>canceled</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Creation time (UTC).</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Transaction hash once broadcast.</summary>
    public string? TransactionHash { get; set; }

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }
}

/// <summary>A deposit.</summary>
public sealed class Deposit
{
    /// <summary>Deposit id.</summary>
    public long Id { get; set; }

    /// <summary>Currency.</summary>
    public string? Currency { get; set; }

    /// <summary>Deposited amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Network.</summary>
    public string? Network { get; set; }

    /// <summary>Number of confirmations.</summary>
    public int Confirmations { get; set; }

    /// <summary>Transaction hash.</summary>
    public string? TransactionHash { get; set; }

    /// <summary>Deposit time (UTC).</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A wallet transaction (deposit, withdrawal, trade, fee, ...).</summary>
public sealed class WalletTransaction
{
    /// <summary>Transaction id.</summary>
    public long Id { get; set; }

    /// <summary>Wallet id.</summary>
    public long WalletId { get; set; }

    /// <summary>Signed amount (negative for debits).</summary>
    public decimal Amount { get; set; }

    /// <summary>Balance after the transaction.</summary>
    public decimal Balance { get; set; }

    /// <summary>Description of the transaction.</summary>
    public string? Description { get; set; }

    /// <summary>Creation time (UTC).</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Result of an instant conversion.</summary>
public sealed class ConversionResult
{
    /// <summary>Conversion id.</summary>
    public long? Id { get; set; }

    /// <summary>Source currency.</summary>
    public string? ConvertFrom { get; set; }

    /// <summary>Destination currency.</summary>
    public string? ConvertTo { get; set; }

    /// <summary>Converted amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Applied price.</summary>
    public decimal Price { get; set; }

    /// <summary>Fee of the conversion.</summary>
    public decimal Fee { get; set; }
}