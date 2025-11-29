using Tidalarr;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// 100% Coverage: TidalProtocol static class testing
/// Tests URL validation, parsing, and construction
/// </summary>
[Trait("scope", "cli")]
public class TidalProtocolTests
{
    [Theory]
    [InlineData("tidal://album/123", true)]
    [InlineData("tidal://track/456", true)]
    [InlineData("TIDAL://ALBUM/789", true)] // Case insensitive
    [InlineData("tidal://artist/999", true)]
    [InlineData("http://tidal.com/album/123", false)]
    [InlineData("spotify://track/123", false)]
    [InlineData("not-a-url", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TidalProtocol_IsValidUrl_WithVariousInputs_ReturnsExpected(string? url, bool expected)
    {
        // Act
        var result = TidalProtocol.IsValidUrl(url!);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("tidal://album/123", "album", "123")]
    [InlineData("tidal://track/456", "track", "456")]
    [InlineData("tidal://artist/789", "artist", "789")]
    [InlineData("tidal://playlist/abc123", "playlist", "abc123")]
    public void TidalProtocol_ParseUrl_WithValidUrls_ReturnsCorrectTypeAndId(string url, string expectedType, string expectedId)
    {
        // Act
        var (type, id) = TidalProtocol.ParseUrl(url);

        // Assert
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("tidal://")]
    [InlineData("tidal://album")]
    [InlineData("tidal://album/")]
    [InlineData("not-a-url")]
    public void TidalProtocol_ParseUrl_WithInvalidUrls_ThrowsArgumentException(string invalidUrl)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => TidalProtocol.ParseUrl(invalidUrl));
    }

    [Fact]
    public void TidalProtocol_BuildAlbumUrl_WithValidId_ReturnsCorrectUrl()
    {
        // Act
        var url = TidalProtocol.BuildAlbumUrl("album123");

        // Assert
        Assert.Equal("tidal://album/album123", url);
        Assert.True(TidalProtocol.IsValidUrl(url));
    }

    [Fact]
    public void TidalProtocol_BuildTrackUrl_WithValidId_ReturnsCorrectUrl()
    {
        // Act
        var url = TidalProtocol.BuildTrackUrl("track456");

        // Assert
        Assert.Equal("tidal://track/track456", url);
        Assert.True(TidalProtocol.IsValidUrl(url));
    }

    [Fact]
    public void TidalProtocol_Constants_HaveExpectedValues()
    {
        // Test protocol metadata
        Assert.Equal("TidalProtocol", TidalProtocol.Name);
        Assert.Equal("Tidal streaming protocol", TidalProtocol.Description);
        Assert.NotEmpty(TidalProtocol.Name);
        Assert.NotEmpty(TidalProtocol.Description);
    }

    [Fact]
    public void TidalProtocol_RoundTrip_BuildThenParse_ReturnsOriginalValues()
    {
        // Arrange
        var albumId = "test_album_123";
        var trackId = "test_track_456";

        // Act - Build URLs then parse them back
        var albumUrl = TidalProtocol.BuildAlbumUrl(albumId);
        var trackUrl = TidalProtocol.BuildTrackUrl(trackId);

        var (albumType, parsedAlbumId) = TidalProtocol.ParseUrl(albumUrl);
        var (trackType, parsedTrackId) = TidalProtocol.ParseUrl(trackUrl);

        // Assert
        Assert.Equal("album", albumType);
        Assert.Equal(albumId, parsedAlbumId);
        Assert.Equal("track", trackType);
        Assert.Equal(trackId, parsedTrackId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void TidalProtocol_BuildAlbumUrl_WithInvalidId_HandlesGracefully(string? invalidId)
    {
        // Act
        var url = TidalProtocol.BuildAlbumUrl(invalidId!);

        // Assert - Should still build URL but may not be useful
        Assert.StartsWith("tidal://album/", url);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void TidalProtocol_BuildTrackUrl_WithInvalidId_HandlesGracefully(string? invalidId)
    {
        // Act
        var url = TidalProtocol.BuildTrackUrl(invalidId!);

        // Assert - Should still build URL but may not be useful
        Assert.StartsWith("tidal://track/", url);
    }
}



