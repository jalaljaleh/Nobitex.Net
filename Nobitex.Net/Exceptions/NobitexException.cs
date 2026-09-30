using System.Text.Json;

namespace Nobitex.Net;

/// <summary>Base class for every exception thrown by Nobitex.Net.</summary>
public class NobitexException : Exception
{
    /// <summary>Creates an empty exception.</summary>
    public NobitexException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public NobitexException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public NobitexException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when Nobitex returns <c>status: "failed"</c> or an unsuccessful HTTP status code.
/// </summary>
public class NobitexApiException : NobitexException
{
    /// <summary>Raw error code returned by Nobitex, for example <c>SmallOrder</c>.</summary>
    public string? ErrorCode { get; }

    /// <summary>Strongly typed error code.</summary>
    public NobitexErrorCode Code { get; }

    /// <summary>HTTP status code of the response (200 when Nobitex reports the failure in-band).</summary>
    public int HttpStatusCode { get; }

    /// <summary>Human readable Persian message returned by Nobitex, when available.</summary>
    public string? ServerMessage { get; }

    /// <summary>Full response body for diagnostics.</summary>
    public string? ResponseBody { get; }

    /// <summary>Creates a new API exception.</summary>
    public NobitexApiException(
        string? errorCode,
        string? message,
        int httpStatusCode,
        string? responseBody = null)
        : base(FormatMessage(errorCode, message, httpStatusCode))
    {
        ErrorCode = errorCode;
        Code = NobitexErrorCodeParser.Parse(errorCode);
        HttpStatusCode = httpStatusCode;
        ServerMessage = message;
        ResponseBody = responseBody;
    }

    private static string FormatMessage(string? errorCode, string? message, int statusCode)
        => $"Nobitex API error [{statusCode}] {errorCode ?? "Unknown"}: {message ?? "no message"}";
}

/// <summary>
/// Thrown when the documented rate limit is exceeded (HTTP 429 / <c>TooManyRequests</c>).
/// </summary>
public sealed class NobitexRateLimitException : NobitexApiException
{
    /// <summary>Seconds the client should wait before retrying, as returned in the <c>backOff</c> field.</summary>
    public TimeSpan BackOff { get; }

    /// <summary>Maximum number of allowed requests in the current window, when provided.</summary>
    public int? Limit { get; }

    /// <summary>Creates a rate-limit exception.</summary>
    public NobitexRateLimitException(string? message, TimeSpan backOff, int? limit, string? body)
        : base(NobitexErrorCode.TooManyRequests.ToString(), message, 429, body)
    {
        BackOff = backOff;
        Limit = limit;
    }
}

/// <summary>Thrown when authentication fails (401/403) or credentials are not configured.</summary>
public sealed class NobitexAuthenticationException : NobitexException
{
    /// <summary>Creates an authentication exception.</summary>
    public NobitexAuthenticationException(string message) : base(message)
    {
    }

    /// <summary>Creates an authentication exception with an inner exception.</summary>
    public NobitexAuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when an order is rejected by the matching engine, for example <c>SmallOrder</c> or
/// <c>OverValueOrder</c>. The exception carries the <see cref="ClientOrderId"/> when it was supplied.
/// </summary>
public sealed class NobitexOrderRejectedException : NobitexApiException
{
    /// <summary>Client order identifier supplied with the rejected request.</summary>
    public string? ClientOrderId { get; }

    /// <summary>Creates an order-rejected exception.</summary>
    public NobitexOrderRejectedException(
        string? errorCode,
        string? message,
        string? clientOrderId,
        string? body = null)
        : base(errorCode, message, 200, body)
    {
        ClientOrderId = clientOrderId;
    }
}

/// <summary>Thrown when the network transport fails or the payload cannot be parsed.</summary>
public sealed class NobitexTransportException : NobitexException
{
    /// <summary>Creates a transport exception.</summary>
    public NobitexTransportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Strongly typed catalogue of the error codes documented by Nobitex.</summary>
public enum NobitexErrorCode
{
    /// <summary>The code was not recognised by this library version.</summary>
    Unknown = 0,

    /// <summary>Too many requests (HTTP 429).</summary>
    TooManyRequests,

    /// <summary>Missing/invalid credentials.</summary>
    AuthenticationFailed,

    /// <summary>The operation is not permitted for the account.</summary>
    Forbidden,

    /// <summary>Requested resource does not exist.</summary>
    NotFound,

    /// <summary>Order price missing or invalid.</summary>
    InvalidOrderPrice,

    /// <summary>Price too far from the current market price (30% band).</summary>
    BadPrice,

    /// <summary>Price condition of the order was not met.</summary>
    PriceConditionFailed,

    /// <summary>Insufficient balance for the requested order value.</summary>
    OverValueOrder,

    /// <summary>Minimum order value not met (3,000,000 IRR / 11 USDT).</summary>
    SmallOrder,

    /// <summary>An identical order was submitted within the last 10 seconds.</summary>
    DuplicateOrder,

    /// <summary>Unknown market pair.</summary>
    InvalidMarketPair,

    /// <summary>The market is temporarily closed.</summary>
    MarketClosed,

    /// <summary>The account is not allowed to trade yet (KYC).</summary>
    TradingUnavailable,

    /// <summary>The feature is in beta and the account is not whitelisted.</summary>
    FeatureUnavailable,

    /// <summary>The supplied clientOrderId is already in use by an open order.</summary>
    DuplicateClientOrderId,

    /// <summary>Neither <c>id</c> nor <c>clientOrderId</c> was provided.</summary>
    NullIdAndClientOrderId,
}

/// <summary>Converts the raw error string returned by Nobitex into a <see cref="NobitexErrorCode"/>.</summary>
public static class NobitexErrorCodeParser
{
    /// <summary>Parses an error code string, returning <see cref="NobitexErrorCode.Unknown"/> when unmatched.</summary>
    public static NobitexErrorCode Parse(string? code) => code switch
    {
        nameof(NobitexErrorCode.TooManyRequests) => NobitexErrorCode.TooManyRequests,
        nameof(NobitexErrorCode.InvalidOrderPrice) => NobitexErrorCode.InvalidOrderPrice,
        nameof(NobitexErrorCode.BadPrice) => NobitexErrorCode.BadPrice,
        nameof(NobitexErrorCode.PriceConditionFailed) => NobitexErrorCode.PriceConditionFailed,
        nameof(NobitexErrorCode.OverValueOrder) => NobitexErrorCode.OverValueOrder,
        nameof(NobitexErrorCode.SmallOrder) => NobitexErrorCode.SmallOrder,
        nameof(NobitexErrorCode.DuplicateOrder) => NobitexErrorCode.DuplicateOrder,
        nameof(NobitexErrorCode.InvalidMarketPair) => NobitexErrorCode.InvalidMarketPair,
        nameof(NobitexErrorCode.MarketClosed) => NobitexErrorCode.MarketClosed,
        nameof(NobitexErrorCode.TradingUnavailable) => NobitexErrorCode.TradingUnavailable,
        nameof(NobitexErrorCode.FeatureUnavailable) => NobitexErrorCode.FeatureUnavailable,
        nameof(NobitexErrorCode.DuplicateClientOrderId) => NobitexErrorCode.DuplicateClientOrderId,
        nameof(NobitexErrorCode.NullIdAndClientOrderId) => NobitexErrorCode.NullIdAndClientOrderId,
        _ => NobitexErrorCode.Unknown,
    };
}