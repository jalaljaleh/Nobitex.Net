using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nobitex.Net;

    public record NobitexProfileResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("profile")] Profile Profile,
        [property: JsonPropertyName("tradeStats")] TradeStats? TradeStats,
        [property: JsonPropertyName("websocketAuthParam")] string? WebsocketAuthParam
    );

    public record Profile(
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName,
        [property: JsonPropertyName("nationalCode")] string? NationalCode,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("phone")] string? Phone,
        [property: JsonPropertyName("mobile")] string? Mobile,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("bankCards")] IReadOnlyList<BankCard>? BankCards,
        [property: JsonPropertyName("bankAccounts")] IReadOnlyList<BankAccount>? BankAccounts,
        [property: JsonPropertyName("verifications")] Verifications? Verifications,
        [property: JsonPropertyName("pendingVerifications")] Verifications? PendingVerifications,
        [property: JsonPropertyName("options")] ProfileOptions? Options,
        [property: JsonPropertyName("withdrawEligible")] bool WithdrawEligible
    );

    public record BankCard(
        [property: JsonPropertyName("number")] string? Number,
        [property: JsonPropertyName("bank")] string? Bank,
        [property: JsonPropertyName("owner")] string? Owner,
        [property: JsonPropertyName("confirmed")] bool Confirmed,
        [property: JsonPropertyName("status")] string? Status
    );

    public record BankAccount(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("number")] string? Number,
        [property: JsonPropertyName("shaba")] string? Shaba,
        [property: JsonPropertyName("bank")] string? Bank,
        [property: JsonPropertyName("owner")] string? Owner,
        [property: JsonPropertyName("confirmed")] bool Confirmed,
        [property: JsonPropertyName("status")] string? Status
    );

    public record Verifications(
        [property: JsonPropertyName("email")] bool Email,
        [property: JsonPropertyName("phone")] bool Phone,
        [property: JsonPropertyName("mobile")] bool Mobile,
        [property: JsonPropertyName("identity")] bool Identity,
        [property: JsonPropertyName("selfie")] bool Selfie,
        [property: JsonPropertyName("bankAccount")] bool BankAccount,
        [property: JsonPropertyName("bankCard")] bool BankCard,
        [property: JsonPropertyName("address")] bool Address,
        [property: JsonPropertyName("city")] bool City,
        [property: JsonPropertyName("nationalSerialNumber")] bool NationalSerialNumber
    );

    public record ProfileOptions(
        [property: JsonPropertyName("fee")] string? Fee,
        [property: JsonPropertyName("feeUsdt")] string? FeeUsdt,
        [property: JsonPropertyName("isManualFee")] bool IsManualFee,
        [property: JsonPropertyName("tfa")] bool Tfa,
        [property: JsonPropertyName("socialLoginEnabled")] bool SocialLoginEnabled
    );

    public record TradeStats(
        [property: JsonPropertyName("monthTradesTotal")] string? MonthTradesTotal,
        [property: JsonPropertyName("monthTradesCount")] int MonthTradesCount
    );















/// <summary>
/// Top-level response for POST /users/limitations
/// </summary>
public record AccountLimitationsResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("limitations")] AccountLimitationsDetail Limitations
);

/// <summary>
/// Limitations payload
/// </summary>
public record AccountLimitationsDetail(
    [property: JsonPropertyName("userLevel")] string UserLevel,
    [property: JsonPropertyName("features")] AccountFeatures Features,
    [property: JsonPropertyName("limits")] AccountLimits Limits
);

/// <summary>
/// Feature flags indicating which actions are allowed for the user
/// </summary>
public record AccountFeatures(
    [property: JsonPropertyName("crypto_trade")] bool CryptoTrade,
    [property: JsonPropertyName("rial_trade")] bool RialTrade,
    [property: JsonPropertyName("coin_deposit")] bool CoinDeposit,
    [property: JsonPropertyName("rial_deposit")] bool RialDeposit,
    [property: JsonPropertyName("coin_withdrawal")] bool CoinWithdrawal,
    [property: JsonPropertyName("rial_withdrawal")] bool RialWithdrawal
);

/// <summary>
/// Limits grouping (each entry contains used and limit amounts in Rials)
/// </summary>
public record AccountLimits(
    [property: JsonPropertyName("withdrawRialDaily")] AccountLimitItem WithdrawRialDaily,
    [property: JsonPropertyName("withdrawCoinDaily")] AccountLimitItem WithdrawCoinDaily,
    [property: JsonPropertyName("withdrawTotalDaily")] AccountLimitItem WithdrawTotalDaily,
    [property: JsonPropertyName("withdrawTotalMonthly")] AccountLimitItem WithdrawTotalMonthly
);

/// <summary>
/// Individual limit item with used amount and allowed limit (both returned as strings by the API).
/// </summary>
public record AccountLimitItem(
    [property: JsonPropertyName("used")] string Used,
    [property: JsonPropertyName("limit")] string Limit
);












/// <summary>
/// Request payload for adding a bank account
/// </summary>
public record AddBankAccountRequest(
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("shaba")] string Shaba,
    [property: JsonPropertyName("bank")] string Bank
);

/// <summary>
/// Generic OK response for simple endpoints that return only status.
/// </summary>
public record AccountAddResponse(
    [property: JsonPropertyName("status")] string Status
);





/// <summary>
/// Request payload for adding a bank card
/// </summary>
public record AddBankCardRequest(
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("bank")] string Bank
);

/// <summary>
/// Generic OK response for simple endpoints that return only status.
/// </summary>
public record CardAddResponse(
    [property: JsonPropertyName("status")] string Status
);














/// <summary>
/// Request payload for generating a blockchain address.
/// Provide either Currency (preferred) or Wallet (legacy). Network is optional.
/// </summary>
public record GenerateAddressRequest(
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("wallet")] string? Wallet,
    [property: JsonPropertyName("network")] string? Network
);

/// <summary>
/// Response for generate-address endpoint.
/// </summary>
public record GenerateAddressResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("address")] string? Address
);