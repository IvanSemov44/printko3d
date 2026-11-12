using System.Buffers.Text;
using System.Security.Cryptography;

namespace Storefront.Application.Carts;

/// <summary>
/// The secret in the cart cookie: 128 random bits written as base64url. Only its SHA-256 hash is stored,
/// so a leaked database gives no access to a cart (decision 0027).
/// </summary>
public static class CartToken
{
    private const int ByteLength = 16;

    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));

    /// <summary>Hashes a token from a cookie. A missing, malformed or non-canonical value gives false, never an error.</summary>
    public static bool TryHash(string? token, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrEmpty(token) || token.Length > 32)
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException)
        {
            return false;
        }

        // Two different strings must never lead to the same cart, so only the canonical spelling is accepted.
        if (bytes.Length != ByteLength || Base64Url.EncodeToString(bytes) != token)
        {
            return false;
        }

        hash = SHA256.HashData(bytes);
        return true;
    }
}
