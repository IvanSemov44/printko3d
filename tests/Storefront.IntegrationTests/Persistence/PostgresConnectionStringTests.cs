using Npgsql;
using Storefront.Infrastructure;

namespace Storefront.IntegrationTests.Persistence;

public sealed class PostgresConnectionStringTests
{
    private static NpgsqlConnectionStringBuilder Parse(string url) =>
        new(PostgresConnectionString.FromUrl(url));

    [Fact]
    public void FromUrl_ReadsHostPortDatabaseUserAndPassword()
    {
        var builder = Parse("postgres://shop_user:s3cret@dpg-abc-a.frankfurt-postgres.render.com:5433/shop_db");

        Assert.Equal("dpg-abc-a.frankfurt-postgres.render.com", builder.Host);
        Assert.Equal(5433, builder.Port);
        Assert.Equal("shop_db", builder.Database);
        Assert.Equal("shop_user", builder.Username);
        Assert.Equal("s3cret", builder.Password);
    }

    [Theory]
    [InlineData("postgres://u:p@dpg-abc-a/db")]
    [InlineData("postgresql://u:p@dpg-abc-a/db")]
    public void FromUrl_UsesTheDefaultPortAndAcceptsBothSchemes(string url)
    {
        var builder = Parse(url);

        Assert.Equal("dpg-abc-a", builder.Host);
        Assert.Equal(5432, builder.Port);
    }

    [Fact]
    public void FromUrl_DecodesSpecialCharactersInTheCredentials()
    {
        var builder = Parse("postgres://us%40er:p%40ss%3Aword%2F%3B@host/db");

        Assert.Equal("us@er", builder.Username);
        Assert.Equal("p@ss:word/;", builder.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://u:p@host/db")]
    [InlineData("postgres://u:p@host")]
    [InlineData("postgres://:p@host/db")]
    public void FromUrl_RejectsAnAddressThatIsNotComplete_WithoutRepeatingThePassword(string url)
    {
        var exception = Assert.Throws<FormatException>(() => PostgresConnectionString.FromUrl(url));

        Assert.DoesNotContain("p@host", exception.Message, StringComparison.Ordinal);
    }
}
