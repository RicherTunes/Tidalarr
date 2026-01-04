using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Tests verifying the ReleaseInfo contract from TidalLidarrParser.ConvertToReleaseInfoStatic.
/// Ensures that releases have correct DownloadProtocol, Guid format, and InfoUrl
/// for proper routing to Tidalarr download client.
/// </summary>
public class TidalLidarrIndexerReleaseContractTests
{
    private static TidalAlbumInfo CreateTestAlbum(string id = "107386922") =>
        new TidalAlbumInfo(
            Id: id,
            Title: "Kind of Blue",
            Artists: ["Miles Davis"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: new DateTime(1959, 8, 17),
            CoverArtId: "cover123",
            IsAvailable: true);

    [Fact]
    public void ConvertToReleaseInfo_SetsDownloadProtocol_ToTidalarrDownloadProtocol()
    {
        // Arrange
        TidalAlbumInfo album = CreateTestAlbum();

        // Act
        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        // Assert - DownloadProtocol must match so Lidarr routes to Tidalarr download client
        Assert.Equal(nameof(TidalarrDownloadProtocol), release.DownloadProtocol);
    }

    [Theory]
    [InlineData("107386922")]
    [InlineData("1")]
    [InlineData("999999999")]
    public void ConvertToReleaseInfo_SetsGuid_InExpectedFormat(string albumId)
    {
        // Arrange
        TidalAlbumInfo album = CreateTestAlbum(albumId);

        // Act
        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        // Assert - Guid format must be tidal:album:{id} for ExtractAlbumIdFromGuid to work
        // (Lidarr may prefix this with {indexerId}_ later, which is handled separately)
        Assert.Equal($"tidal:album:{albumId}", release.Guid);
    }

    [Fact]
    public void ConvertToReleaseInfo_SetsInfoUrl_ToValidTidalUrl()
    {
        // Arrange
        string albumId = "107386922";
        TidalAlbumInfo album = CreateTestAlbum(albumId);

        // Act
        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        // Assert - InfoUrl must be valid tidal.com URL for ExtractAlbumIdFromInfoUrl fallback
        Assert.Equal($"https://tidal.com/browse/album/{albumId}", release.InfoUrl);
    }

    [Fact]
    public void ConvertToReleaseInfo_SetsArtistAndAlbum()
    {
        // Arrange
        TidalAlbumInfo album = CreateTestAlbum();

        // Act
        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        // Assert - Artist/Album fields must be set for Lidarr matching
        // (Title formatting is implementation detail, not tested)
        Assert.Equal("Miles Davis", release.Artist);
        Assert.Equal("Kind of Blue", release.Album);
        Assert.False(string.IsNullOrWhiteSpace(release.Title));
    }

    [Fact]
    public void ConvertToReleaseInfo_SetsDownloadUrl_InExpectedFormat()
    {
        // Arrange
        string albumId = "107386922";
        TidalAlbumInfo album = CreateTestAlbum(albumId);

        // Act
        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        // Assert - DownloadUrl format used by download client
        Assert.Equal($"tidal://album/{albumId}", release.DownloadUrl);
    }

    [Fact]
    public void ConvertToReleaseInfo_NullAlbum_ReturnsNull()
    {
        // Act
        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(null!);

        // Assert
        Assert.Null(release);
    }
}
