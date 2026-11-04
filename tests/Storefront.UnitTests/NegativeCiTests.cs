namespace Storefront.UnitTests;

public class NegativeCiTests
{
    [Fact]
    public void Deliberately_failing_test()
    {
        Assert.Equal(1, 2);
    }
}
