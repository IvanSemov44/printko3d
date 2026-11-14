using System.Security.Cryptography;

namespace Storefront.Application.Orders;

/// <summary>The key in the link of the emails: it opens the order of the buyer without an account (decision 0036).</summary>
public static class OrderAccessToken
{
    private const int Bytes = 32;

    /// <summary>256 random bits, written in the URL-safe form of base64 (43 characters).</summary>
    public static string Create() => Base64Url(RandomNumberGenerator.GetBytes(Bytes));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
