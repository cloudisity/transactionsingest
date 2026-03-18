using TransactionsIngest.Services;
using Xunit;

namespace TransactionsIngest.Tests;

public class CardNumberHelperTests
{
    [Fact]
    public void Hash_ReturnsDeterministicResult()
    {
        var hash1 = CardNumberHelper.Hash("4111111111111111");
        var hash2 = CardNumberHelper.Hash("4111111111111111");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentInputs_DifferentHashes()
    {
        var hash1 = CardNumberHelper.Hash("4111111111111111");
        var hash2 = CardNumberHelper.Hash("4000000000000002");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_Returns64CharHexString()
    {
        var hash = CardNumberHelper.Hash("4111111111111111");
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]+$", hash);
    }

    [Fact]
    public void Last4_ReturnsLastFourDigits()
    {
        Assert.Equal("1111", CardNumberHelper.Last4("4111111111111111"));
        Assert.Equal("0002", CardNumberHelper.Last4("4000000000000002"));
    }

    [Fact]
    public void Last4_ShortInput_ReturnsAsIs()
    {
        Assert.Equal("12", CardNumberHelper.Last4("12"));
    }
}