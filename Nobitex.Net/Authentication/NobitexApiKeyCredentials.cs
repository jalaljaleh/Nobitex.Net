using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Nobitex.Net.Authentication;

/// <summary>Result of signing a single request with an Ed25519 API key.</summary>
/// <param name="Key">The public API key, sent in the <c>Nobitex-Key</c> header.</param>
/// <param name="Timestamp">Unix timestamp (seconds, UTC) sent in the <c>Nobitex-Timestamp</c> header.</param>
/// <param name="Signature">Base64 encoded Ed25519 signature sent in the <c>Nobitex-Signature</c> header.</param>
public sealed record NobitexRequestSignature(string Key, string Timestamp, string Signature);

/// <summary>
/// Abstraction over the request signing algorithm. Implement this interface to delegate
/// signing to an HSM/KMS or any other external signer.
/// </summary>
public interface INobitexRequestSigner
{
    /// <summary>Signs a request.</summary>
    /// <param name="timestamp">Unix timestamp in seconds (UTC).</param>
    /// <param name="httpMethod">HTTP verb, for example <c>GET</c> or <c>POST</c>.</param>
    /// <param name="url">Full request path including query string, e.g. <c>/market/orders/list?fromId=123</c>.</param>
    /// <param name="body">Raw request body (empty for <c>GET</c> requests).</param>
    /// <returns>The signature data required by the Nobitex API.</returns>
    NobitexRequestSignature Sign(string timestamp, string httpMethod, string url, string? body);
}

/// <summary>
/// Stores a Nobitex API key pair (READ / TRADE / WITHDRAW permissions) and exposes the
/// Ed25519 request signer required by the Nobitex v2 API.
/// </summary>
/// <remarks>
/// <para>
/// Nobitex signs every request as:
/// </para>
/// <code>
/// signature = base64( Ed25519( timestamp + method + url + body ) )
/// </code>
/// <para>
/// The <b>private</b> key is returned only once when the key is created, therefore it must be
/// stored safely (Azure Key Vault, AWS Secrets Manager, DPAPI, ...). Never commit it to source control.
/// </para>
/// </remarks>
public sealed class NobitexApiKeyCredentials : INobitexRequestSigner
{
    private readonly Ed25519PrivateKeyParameters? _privateKey;
    private readonly INobitexRequestSigner? _externalSigner;
    private readonly string _publicKey;

    /// <summary>Creates a new API-key credential set.</summary>
    /// <param name="publicKeyBase64">The public key (<c>key.key</c> in the create-key response).</param>
    /// <param name="privateKeyBase64">The one-time returned private key (<c>privateKey</c> field), 32 bytes base64.</param>
    /// <exception cref="ArgumentException">Thrown when the base64 payloads are malformed.</exception>
    public NobitexApiKeyCredentials(string publicKeyBase64, string privateKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(publicKeyBase64))
        {
            throw new ArgumentException("Public key is required.", nameof(publicKeyBase64));
        }

        if (string.IsNullOrWhiteSpace(privateKeyBase64))
        {
            throw new ArgumentException("Private key is required.", nameof(privateKeyBase64));
        }

        var rawPrivateKey = Convert.FromBase64String(privateKeyBase64);

        if (rawPrivateKey.Length != 32)
        {
            throw new ArgumentException(
                $"Ed25519 private key must be 32 bytes long, but {rawPrivateKey.Length} bytes were provided.",
                nameof(privateKeyBase64));
        }

        _publicKey = publicKeyBase64.Trim();
        _privateKey = new Ed25519PrivateKeyParameters(rawPrivateKey, 0);
    }

    /// <summary>
    /// Creates credentials that delegate signing to an external implementation such as an
    /// HSM, Azure Key Vault or AWS KMS. The private key never leaves the secure store.
    /// </summary>
    /// <param name="publicKey">The public API key.</param>
    /// <param name="externalSigner">The signer implementation.</param>
    public NobitexApiKeyCredentials(string publicKey, INobitexRequestSigner externalSigner)
    {
        _publicKey = string.IsNullOrWhiteSpace(publicKey)
            ? throw new ArgumentException("Public key is required.", nameof(publicKey))
            : publicKey.Trim();
        _externalSigner = externalSigner ?? throw new ArgumentNullException(nameof(externalSigner));
    }

    /// <summary>Factory for credentials backed by an external (HSM/KMS) signer.</summary>
    public static NobitexApiKeyCredentials FromSigner(string publicKey, INobitexRequestSigner externalSigner)
        => new(publicKey, externalSigner);

    /// <summary>The public API key.</summary>
    public string PublicKey => _publicKey;

    /// <summary>Returns <c>true</c> when a usable signing backend is configured.</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(_publicKey) && (_privateKey is not null || _externalSigner is not null);

    /// <inheritdoc />
    public NobitexRequestSignature Sign(string timestamp, string httpMethod, string url, string? body)
    {
        if (_externalSigner is not null)
        {
            return _externalSigner.Sign(timestamp, httpMethod, url, body);
        }

        var message = Encoding.UTF8.GetBytes(
            $"{timestamp}{httpMethod.ToUpperInvariant()}{url}{body ?? string.Empty}");

        var signer = SignerUtilities.GetSigner("Ed25519");
        signer.Init(true, _privateKey!);
        signer.BlockUpdate(message, 0, message.Length);

        var signature = signer.GenerateSignature();

        return new NobitexRequestSignature(
            Key: _publicKey,
            Timestamp: timestamp,
            Signature: Convert.ToBase64String(signature));
    }

    /// <summary>
    /// Derives the public key from a private key. Useful to validate that the stored pair matches
    /// the key registered on Nobitex.
    /// </summary>
    /// <param name="privateKeyBase64">Base64 encoded 32-byte private key.</param>
    /// <returns>Base64 encoded public key.</returns>
    public static string DerivePublicKey(string privateKeyBase64)
    {
        var raw = Convert.FromBase64String(privateKeyBase64);
        var privateKey = new Ed25519PrivateKeyParameters(raw, 0);
        return Convert.ToBase64String(privateKey.GeneratePublicKey().GetEncoded());
    }
}

/// <summary>
/// Static factory that builds the signed headers for a request. Kept separate from
/// <see cref="NobitexApiKeyCredentials"/> so it can be unit tested and time-travelled in tests.
/// </summary>
public static class NobitexRequestSigner
{
    /// <summary>Signs a request using the provided credentials and the current UTC time.</summary>
    public static NobitexRequestSignature Sign(
        INobitexRequestSigner signer,
        string httpMethod,
        string url,
        string? body,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(signer);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        return signer.Sign(timestamp, httpMethod, url, body);
    }
}