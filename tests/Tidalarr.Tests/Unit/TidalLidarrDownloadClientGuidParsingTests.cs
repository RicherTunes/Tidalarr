using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Tests for GUID parsing in TidalLidarrDownloadClient.
/// Ensures album IDs are correctly extracted from both prefixed and unprefixed GUID formats.
/// </summary>
public class TidalLidarrDownloadClientGuidParsingTests
{
    [Theory]
    [InlineData("tidal:album:107386922", "107386922")]
    [InlineData("tidal:album:12345678", "12345678")]
    [InlineData("tidal:album:1", "1")]
    public void ExtractAlbumIdFromGuid_UnprefixedFormat_ReturnsAlbumId(string guid, string expectedAlbumId)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Equal(expectedAlbumId, result);
    }

    [Theory]
    [InlineData("2_tidal:album:107386922", "107386922")]
    [InlineData("1_tidal:album:12345678", "12345678")]
    [InlineData("99_tidal:album:999999", "999999")]
    [InlineData("123_tidal:album:1", "1")]
    public void ExtractAlbumIdFromGuid_PrefixedFormat_ReturnsAlbumId(string guid, string expectedAlbumId)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Equal(expectedAlbumId, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractAlbumIdFromGuid_NullOrEmpty_ReturnsNull(string? guid)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("qobuz:album:12345678")]
    [InlineData("spotify:album:abc123")]
    [InlineData("random-string")]
    [InlineData("2_qobuz:album:12345678")]
    public void ExtractAlbumIdFromGuid_NonTidalFormat_ReturnsNull(string guid)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("TIDAL:ALBUM:107386922", "107386922")]
    [InlineData("Tidal:Album:12345678", "12345678")]
    [InlineData("2_TIDAL:ALBUM:107386922", "107386922")]
    public void ExtractAlbumIdFromGuid_CaseInsensitive_ReturnsAlbumId(string guid, string expectedAlbumId)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Equal(expectedAlbumId, result);
    }

    [Theory]
    [InlineData("000_tidal:album:107386922", "107386922")]  // Leading zeros
    [InlineData("0_tidal:album:12345678", "12345678")]       // Single zero
    [InlineData("2147483647_tidal:album:999999", "999999")]  // Max int32
    [InlineData("9999999999_tidal:album:1", "1")]            // Beyond int32 (still valid digits)
    public void ExtractAlbumIdFromGuid_EdgeCasePrefixes_ReturnsAlbumId(string guid, string expectedAlbumId)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Equal(expectedAlbumId, result);
    }

    [Theory]
    [InlineData("foo_tidal:album:123")]           // Non-numeric prefix
    [InlineData("abc_2_tidal:album:123")]         // Mixed prefix
    [InlineData("_tidal:album:123")]              // Empty prefix (no digits)
    [InlineData("2a_tidal:album:123")]            // Alphanumeric prefix
    public void ExtractAlbumIdFromGuid_NonNumericPrefix_ReturnsNull(string guid)
    {
        // Non-numeric prefixes should not be stripped, and the resulting string
        // won't match "tidal:album:{id}" format, so should return null
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Null(result);
    }
}
