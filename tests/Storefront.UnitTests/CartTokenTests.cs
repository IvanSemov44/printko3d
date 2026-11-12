using System.Security.Cryptography;
using Storefront.Application.Carts;

namespace Storefront.UnitTests;

public sealed class CartTokenTests
{
    [Fact]
    public void Create_GivesDifferentTokens_WhichHashTo32Bytes()
    {
        var tokens = Enumerable.Range(0, 20).Select(_ => CartToken.Create()).ToList();

        Assert.Equal(20, tokens.Distinct().Count());
        Assert.All(tokens, token =>
        {
            Assert.True(CartToken.TryHash(token, out var hash));
            Assert.Equal(SHA256.HashSizeInBytes, hash.Length);
        });
    }

    [Fact]
    public void TryHash_IsTheSha256OfTheDecodedBytes_AndNeverOfTheTokenText()
    {
        var bytes = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.True(CartToken.TryHash(token, out var hash));

        Assert.Equal(SHA256.HashData(bytes), hash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAB")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    public void TryHash_RejectsMissing_Malformed_AndNonCanonicalValues(string? token)
    {
        Assert.False(CartToken.TryHash(token, out var hash));
        Assert.Empty(hash);
    }
}
