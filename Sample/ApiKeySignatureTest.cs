using Nobitex.Net.Authentication;
using System;

// Test to verify API Key authentication signature generation with URL-safe Base64
namespace Sample
{
    internal class ApiKeySignatureTest
    {
        /// <summary>
        /// Test that URL-safe Base64 encoding is used for signatures.
        /// </summary>
        public static void TestUrlSafeBase64Encoding()
        {
            // Example API key pair (for testing only)
            const string publicKey = "public-key";
            const string privateKeyBase64 = "private-key";// "secret-key" // Example 32-byte private key

            try
            {
                var credentials = new NobitexApiKeyCredentials(publicKey, privateKeyBase64);

                // Test signature generation
                var timestamp = "1234567890";
                var httpMethod = "POST";
                var url = "/market/orders/cancel-old";
                var body = "{\"hours\":2.4}";

                var signature = credentials.Sign(timestamp, httpMethod, url, body);

                Console.WriteLine("=== API Key Authentication Test ===");
                Console.WriteLine($"Public Key: {signature.Key}");
                Console.WriteLine($"Timestamp: {signature.Timestamp}");
                Console.WriteLine($"Signature: {signature.Signature}");
                Console.WriteLine();

                // Verify URL-safe Base64 encoding
                bool hasPlus = signature.Signature.Contains("+");
                bool hasSlash = signature.Signature.Contains("/");
                bool hasEqualPadding = signature.Signature.Contains("=");

                Console.WriteLine("=== URL-Safe Base64 Validation ===");
                Console.WriteLine($"Contains '+' (should be false): {hasPlus}");
                Console.WriteLine($"Contains '/' (should be false): {hasSlash}");
                Console.WriteLine($"Contains '=' padding (should be false): {hasEqualPadding}");
                Console.WriteLine();

                if (!hasPlus && !hasSlash && !hasEqualPadding)
                {
                    Console.WriteLine("✓ Signature is correctly URL-safe Base64 encoded");
                }
                else
                {
                    Console.WriteLine("✗ Signature is NOT properly URL-safe Base64 encoded");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
