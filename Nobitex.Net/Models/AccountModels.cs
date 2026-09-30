using System.Text.Json.Serialization;

namespace Nobitex.Net.Models;

/// <summary>Result of <c>POST /auth/login/</c>.</summary>
public sealed class LoginResult
{
    /// <summary>API token to be sent as <c>Authorization: Token …</c>.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Device identifier; sending it on subsequent logins avoids the 1-hour withdrawal limitation.</summary>
    public string? Device { get; set; }
}

/// <summary>Profile of the authenticated user.</summary>
public sealed class UserProfile
{
    /// <summary>User id.</summary>
    public long Id { get; set; }

    /// <summary>E-mail address.</summary>
    public string? Email { get; set; }

    /// <summary>First name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Last name.</summary>
    public string? LastName { get; set; }

    /// <summary>National code.</summary>
    public string? NationalCode { get; set; }

    /// <summary>Mobile phone number.</summary>
    public string? Cellphone { get; set; }

    /// <summary>Account creation time.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Indicates whether KYC has been approved.</summary>
    public bool IsActive { get; set; }
}

/// <summary>User preferences.</summary>
public sealed class UserPreferences
{
    /// <summary>Preferred UI language.</summary>
    public string? Language { get; set; }

    /// <summary>Preferred display currency.</summary>
    public string? Currency { get; set; }

    /// <summary>Theme.</summary>
    public string? Theme { get; set; }
}

/// <summary>KYC verification status.</summary>
public sealed class VerificationStatus
{
    /// <summary>Overall status, e.g. <c>accepted</c>, <c>pending</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Is trading allowed?</summary>
    public bool TradingAllowed { get; set; }

    /// <summary>Is withdrawal allowed?</summary>
    public bool WithdrawAllowed { get; set; }
}

/// <summary>Account limitations.</summary>
public sealed class AccountLimitations
{
    /// <summary>Withdrawal limitation reason, when present.</summary>
    public string? WithdrawalLimitation { get; set; }

    /// <summary>Trading limitation reason, when present.</summary>
    public string? TradingLimitation { get; set; }

    /// <summary>Raw limitation payload.</summary>
    public Dictionary<string, object>? Raw { get; set; }
}

/// <summary>A user notification.</summary>
public sealed class UserNotification
{
    /// <summary>Notification id.</summary>
    public long Id { get; set; }

    /// <summary>Title.</summary>
    public string? Title { get; set; }

    /// <summary>Body text.</summary>
    public string? Body { get; set; }

    /// <summary>Was the notification read?</summary>
    public bool IsRead { get; set; }

    /// <summary>Creation time.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Connection token for the private WebSocket channels.</summary>
public sealed class WebSocketToken
{
    /// <summary>JWT token used in the Centrifugo <c>connect</c> message.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Indicates whether the token is present.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Token);
}

/// <summary>An API key of the user.</summary>
public sealed class ApiKey
{
    /// <summary>Public key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Friendly name.</summary>
    public string? Name { get; set; }

    /// <summary>Description.</summary>
    public string? Description { get; set; }

    /// <summary>Permissions, e.g. <c>READ,TRADE</c>.</summary>
    public string? Permissions { get; set; }

    /// <summary>Parsed permissions.</summary>
    public ApiKeyPermissions ParsedPermissions => EnumExtensions.ParsePermissions(Permissions);

    /// <summary>Allowed IP addresses.</summary>
    [JsonPropertyName("ipAddressesWhitelist")]
    public List<string>? IpAddressesWhitelist { get; set; }

    /// <summary>Expiration date.</summary>
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset? ExpirationDate { get; set; }

    /// <summary>Creation time.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Response of <c>POST /apikeys/create</c>. The private key is returned only once!</summary>
public sealed class CreatedApiKey
{
    /// <summary>Created key.</summary>
    public ApiKey? Key { get; set; }

    /// <summary>Private key (base64, 32 bytes). Store it safely immediately.</summary>
    [JsonPropertyName("privateKey")]
    public string? PrivateKey { get; set; }
}