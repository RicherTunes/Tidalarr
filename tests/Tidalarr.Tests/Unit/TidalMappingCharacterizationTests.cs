using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Characterization tests for the DTO → Domain → StreamingTrack mapping chain.
/// These tests lock the behavior of the mapping layer to prevent regressions
/// where deserialization succeeds but mapping drops/transforms values incorrectly.
///
/// Mapping chain:
///   TidalTrackDto.id (long) → TidalTrackInfo.Id (string) → StreamingTrack.Id (string)
/// </summary>
public class TidalMappingCharacterizationTests
{
    private readonly TidalModelMapper _mapper = new();

    #region TidalTrackInfo → StreamingTrack

    [Fact]
    public void ToStreamingTrack_Id_FlowsCorrectly()
    {
        // Arrange
        var trackInfo = new TidalTrackInfo(
            Id: "123456789",
            Title: "Test Track",
            Artists: ["Test Artist"],
            AlbumId: "987654321",
            AlbumTitle: "Test Album",
            TrackNumber: 5,
            Duration: 240,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: new DateTime(2024, 1, 15));

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert - verify Id flows through unchanged
        Assert.Equal("123456789", result.Id);
        Assert.Equal("123456789", result.ExternalIds["tidal"]);
        Assert.Equal("123456789", result.Metadata["tidal_id"]);
    }

    [Fact]
    public void ToStreamingTrack_Title_FlowsCorrectly()
    {
        // Arrange
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "So What (feat. John Coltrane)",
            Artists: ["Miles Davis"],
            AlbumId: "2",
            AlbumTitle: "Kind of Blue",
            TrackNumber: 1,
            Duration: 562,
            Quality: TidalQuality.HiRes,
            IsAvailable: true,
            ReleaseDate: new DateTime(1959, 8, 17));

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert
        Assert.Equal("So What (feat. John Coltrane)", result.Title);
    }

    [Fact]
    public void ToStreamingTrack_Artist_FlowsCorrectly()
    {
        // Arrange - multiple artists
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: ["Miles Davis", "John Coltrane", "Cannonball Adderley"],
            AlbumId: "2",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 300,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert - artists joined with comma
        Assert.Equal("Miles Davis, John Coltrane, Cannonball Adderley", result.Artist.Name);
        // First artist used as ID
        Assert.Equal("Miles Davis", result.Artist.Id);
    }

    [Fact]
    public void ToStreamingTrack_Album_FlowsCorrectly()
    {
        // Arrange
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "987654321",
            AlbumTitle: "Kind of Blue (Remastered)",
            TrackNumber: 3,
            Duration: 300,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert
        Assert.Equal("987654321", result.Album.Id);
        Assert.Equal("Kind of Blue (Remastered)", result.Album.Title);
        Assert.Equal("987654321", result.Metadata["album_id"]);
    }

    [Fact]
    public void ToStreamingTrack_TrackNumber_FlowsCorrectly()
    {
        // Arrange
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "2",
            AlbumTitle: "Album",
            TrackNumber: 7,
            Duration: 300,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert
        Assert.Equal(7, result.TrackNumber);
    }

    [Fact]
    public void ToStreamingTrack_Duration_FlowsCorrectly()
    {
        // Arrange
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "2",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 562, // 9:22
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(562), result.Duration);
    }

    [Fact]
    public void ToStreamingTrack_Quality_MapsToStreamingQuality()
    {
        // Arrange
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "2",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 300,
            Quality: TidalQuality.HiRes,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert - HiRes maps to HI_RES id
        Assert.Contains(result.AvailableQualities, q => q.Id == "HI_RES");
    }

    #endregion

    #region TidalAlbumInfo → StreamingAlbum

    [Fact]
    public void ToStreamingAlbum_Id_FlowsCorrectly()
    {
        // Arrange
        var albumInfo = new TidalAlbumInfo(
            Id: "246813579",
            Title: "Kind of Blue",
            Artists: ["Miles Davis"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: new DateTime(1959, 8, 17),
            CoverArtId: "cover-123",
            IsAvailable: true);

        // Act
        var result = _mapper.ToStreamingAlbum(albumInfo);

        // Assert
        Assert.Equal("246813579", result.Id);
        Assert.Equal("246813579", result.ExternalIds["tidal"]);
        Assert.Equal("246813579", result.Metadata["tidal_id"]);
    }

    [Fact]
    public void ToStreamingAlbum_ExternalUrls_IncludesTidalUrl()
    {
        // Arrange
        var albumInfo = new TidalAlbumInfo(
            Id: "12345",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: DateTime.Now,
            CoverArtId: null,
            IsAvailable: true);

        // Act
        var result = _mapper.ToStreamingAlbum(albumInfo);

        // Assert
        Assert.Equal("https://tidal.com/browse/album/12345", result.ExternalUrls["tidal"]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToStreamingTrack_EmptyArtists_NormalizesToUnknownArtist()
    {
        // Arrange
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: [], // Empty
            AlbumId: "2",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 300,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert - empty artists normalized to "Unknown Artist" to prevent empty tags/folder names
        Assert.Equal("Unknown Artist", result.Artist.Name);
        Assert.Equal("Unknown Artist", result.Artist.Id);
    }

    [Fact]
    public void ToStreamingTrack_NullArtists_NormalizesToUnknownArtist()
    {
        // Arrange - using null for artists (if somehow passed)
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: null!,
            AlbumId: "2",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 300,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert - null artists normalized to "Unknown Artist" to prevent empty tags/folder names
        Assert.Equal("Unknown Artist", result.Artist.Name);
    }

    [Fact]
    public void ToStreamingTrack_DiscNumber_DefaultsToOne()
    {
        // Arrange - TidalTrackInfo doesn't have disc number, should default to 1
        var trackInfo = new TidalTrackInfo(
            Id: "1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "2",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 300,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Now);

        // Act
        var result = _mapper.ToStreamingTrack(trackInfo);

        // Assert
        Assert.Equal(1, result.DiscNumber);
    }

    #endregion
}
