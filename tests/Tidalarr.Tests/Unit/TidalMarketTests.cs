using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit;

public sealed class TidalMarketTests
{
    [Theory]
    [InlineData("UK", "GB")]   // the common mistake -> ISO; Tidal rejects "UK"
    [InlineData("uk", "GB")]
    [InlineData("us", "US")]
    [InlineData("GB", "GB")]
    [InlineData("de", "DE")]
    [InlineData(" fr ", "FR")]
    [InlineData("", "US")]      // empty -> default
    [InlineData(null, "US")]
    [InlineData("USA", "US")]   // invalid length -> default
    [InlineData("U1", "US")]    // non-letter -> default
    public void Normalize_mapsToIsoOrDefault(string? input, string expected)
    {
        Assert.Equal(expected, TidalMarket.Normalize(input));
    }

    [Theory]
    [InlineData("US", true)]
    [InlineData("GB", true)]
    [InlineData("UK", true)]    // accepted (normalized to GB on use)
    [InlineData("jp", true)]
    [InlineData("USA", false)]
    [InlineData("U", false)]
    [InlineData("U1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_requiresTwoLetters(string? input, bool expected)
    {
        Assert.Equal(expected, TidalMarket.IsValid(input));
    }
}
