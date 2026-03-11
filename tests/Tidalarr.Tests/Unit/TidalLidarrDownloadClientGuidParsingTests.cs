using NzbDrone.Core.Parser.Model;
using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Tests for GUID parsing and quality extraction in TidalLidarrDownloadClient.
/// Ensures album IDs are correctly extracted from both prefixed and unprefixed GUID formats,
/// including the 4-part quality-suffixed format (tidal:album:ID:Quality).
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

    // --- Quality-suffixed GUID format (tidal:album:ID:Quality) ---

    [Theory]
    [InlineData("tidal:album:107386922:Lossless", "107386922")]
    [InlineData("tidal:album:12345678:HiRes", "12345678")]
    [InlineData("tidal:album:999:Low", "999")]
    [InlineData("tidal:album:1:High", "1")]
    public void ExtractAlbumIdFromGuid_QualitySuffixed_ReturnsAlbumIdIgnoringQuality(string guid, string expectedAlbumId)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Equal(expectedAlbumId, result);
    }

    [Theory]
    [InlineData("2_tidal:album:107386922:Lossless", "107386922")]
    [InlineData("1_tidal:album:12345678:HiRes", "12345678")]
    public void ExtractAlbumIdFromGuid_PrefixedAndQualitySuffixed_ReturnsAlbumId(string guid, string expectedAlbumId)
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid(guid);

        Assert.Equal(expectedAlbumId, result);
    }

    [Fact]
    public void ExtractAlbumIdFromGuid_EmptyIdSegment_ReturnsNull()
    {
        // "tidal:album:" has an empty 3rd segment — semantically invalid
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid("tidal:album:");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractAlbumIdFromGuid_WhitespaceIdSegment_ReturnsNull()
    {
        string? result = TidalLidarrDownloadClient.ExtractAlbumIdFromGuid("tidal:album:  ");

        Assert.Null(result);
    }
}

/// <summary>
/// Tests for ExtractQualityFromRelease in TidalLidarrDownloadClient.
/// Ensures quality is correctly extracted from both DownloadUrl and GUID fallback.
/// </summary>
public class TidalLidarrDownloadClientQualityExtractionTests
{
    [Theory]
    [InlineData("tidal://album/123?quality=Lossless", TidalQuality.Lossless)]
    [InlineData("tidal://album/123?quality=HiRes", TidalQuality.HiRes)]
    [InlineData("tidal://album/123?quality=High", TidalQuality.High)]
    [InlineData("tidal://album/123?quality=Low", TidalQuality.Low)]
    public void ExtractQualityFromRelease_DownloadUrl_ReturnsQuality(string downloadUrl, TidalQuality expected)
    {
        ReleaseInfo release = new() { DownloadUrl = downloadUrl, Guid = "tidal:album:123" };

        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(release);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("tidal://album/123?quality=lossless", TidalQuality.Lossless)]
    [InlineData("tidal://album/123?quality=HIRES", TidalQuality.HiRes)]
    public void ExtractQualityFromRelease_DownloadUrl_CaseInsensitive(string downloadUrl, TidalQuality expected)
    {
        ReleaseInfo release = new() { DownloadUrl = downloadUrl, Guid = "tidal:album:123" };

        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(release);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("tidal:album:123:Lossless", TidalQuality.Lossless)]
    [InlineData("tidal:album:123:HiRes", TidalQuality.HiRes)]
    [InlineData("tidal:album:123:High", TidalQuality.High)]
    [InlineData("tidal:album:123:Low", TidalQuality.Low)]
    public void ExtractQualityFromRelease_GuidFallback_ReturnsQuality(string guid, TidalQuality expected)
    {
        // No DownloadUrl, so should fall back to GUID parsing
        ReleaseInfo release = new() { Guid = guid };

        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(release);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractQualityFromRelease_DownloadUrlTakesPrecedenceOverGuid()
    {
        // DownloadUrl says HiRes, GUID says Lossless — DownloadUrl should win
        ReleaseInfo release = new()
        {
            DownloadUrl = "tidal://album/123?quality=HiRes",
            Guid = "tidal:album:123:Lossless"
        };

        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(release);

        Assert.Equal(TidalQuality.HiRes, result);
    }

    [Fact]
    public void ExtractQualityFromRelease_NoQualityAnywhere_ReturnsNull()
    {
        ReleaseInfo release = new()
        {
            DownloadUrl = "tidal://album/123",
            Guid = "tidal:album:123"
        };

        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(release);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractQualityFromRelease_NullRelease_ReturnsNull()
    {
        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(null);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractQualityFromRelease_InvalidQualityString_ReturnsNull()
    {
        ReleaseInfo release = new()
        {
            DownloadUrl = "tidal://album/123?quality=UltraHD",
            Guid = "tidal:album:123:UltraHD"
        };

        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(release);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractQualityFromRelease_MalformedDownloadUrl_DoesNotThrow()
    {
        ReleaseInfo release = new()
        {
            DownloadUrl = "not-a-valid-url",
            Guid = "tidal:album:123:Lossless"
        };

        // Should fall through to GUID parsing without crashing
        TidalQuality? result = TidalLidarrDownloadClient.ExtractQualityFromRelease(release);

        Assert.Equal(TidalQuality.Lossless, result);
    }
}
