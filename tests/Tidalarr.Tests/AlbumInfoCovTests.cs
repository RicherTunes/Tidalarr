using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalAlbumInfo record.
/// Source: src/Tidalarr/Core/Models/TidalAlbumInfo.cs
/// </summary>
public class AlbumInfoCovTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var expectedId = "album-123";
        var expectedTitle = "Test Album";
        var expectedArtists = new List<string> { "Artist One", "Artist Two" };
        var expectedTracks = new List<TidalTrackInfo>
        {
            new(
                Id: "track-1",
                Title: "Track One",
                Artists: new List<string> { "Artist One" },
                AlbumId: "album-123",
                AlbumTitle: "Test Album",
                TrackNumber: 1,
                Duration: 180,
                Quality: TidalQuality.High,
                IsAvailable: true,
                ReleaseDate: new DateTime(2024, 1, 1)
            )
        };
        var expectedQualities = new List<TidalQuality> { TidalQuality.High, TidalQuality.Lossless };
        var expectedReleaseDate = new DateTime(2024, 3, 15);
        var expectedCoverArtId = "cover-uuid-456";
        var expectedIsAvailable = true;
        var expectedPrimaryArtistId = 12345L;

        // Act
        var album = new TidalAlbumInfo(
            Id: expectedId,
            Title: expectedTitle,
            Artists: expectedArtists,
            Tracks: expectedTracks,
            AvailableQualities: expectedQualities,
            ReleaseDate: expectedReleaseDate,
            CoverArtId: expectedCoverArtId,
            IsAvailable: expectedIsAvailable,
            PrimaryArtistId: expectedPrimaryArtistId
        );

        // Assert - Each property verified individually
        // Source line 9: string Id
        Assert.Equal(expectedId, album.Id);
        // Source line 10: string Title
        Assert.Equal(expectedTitle, album.Title);
        // Source line 11: IReadOnlyList<string> Artists
        Assert.Equal(expectedArtists, album.Artists);
        // Source line 12: IReadOnlyList<TidalTrackInfo> Tracks
        Assert.Single(album.Tracks);
        Assert.Equal("track-1", album.Tracks[0].Id);
        // Source line 13: IReadOnlyList<TidalQuality> AvailableQualities
        Assert.Equal(2, album.AvailableQualities.Count);
        Assert.Contains(TidalQuality.High, album.AvailableQualities);
        Assert.Contains(TidalQuality.Lossless, album.AvailableQualities);
        // Source line 14: DateTime ReleaseDate
        Assert.Equal(expectedReleaseDate, album.ReleaseDate);
        // Source line 15: string CoverArtId
        Assert.Equal(expectedCoverArtId, album.CoverArtId);
        // Source line 16: bool IsAvailable
        Assert.True(album.IsAvailable);
        // Source line 17: long? PrimaryArtistId
        Assert.Equal(expectedPrimaryArtistId, album.PrimaryArtistId);
    }

    [Fact]
    public void Constructor_WithoutOptionalPrimaryArtistId_SetsNull()
    {
        // Arrange & Act
        var album = new TidalAlbumInfo(
            Id: "album-no-artist-id",
            Title: "Album Without Artist ID",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.Low },
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover-id",
            IsAvailable: false
        );

        // Assert - Source line 17: long? PrimaryArtistId = null
        Assert.Null(album.PrimaryArtistId);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equality_SameInstance_ReturnsTrue()
    {
        // Arrange - Create album with empty collections to avoid list reference comparison issues
        var album = new TidalAlbumInfo(
            Id: "album-1",
            Title: "Album Title",
            Artists: new List<string>(),
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: new DateTime(2024, 1, 1),
            CoverArtId: "cover-1",
            IsAvailable: true,
            PrimaryArtistId: 100L
        );

        // Act & Assert - Same instance equals itself
        Assert.Equal(album, album);
    }

    [Fact]
    public void Equality_SameSimpleValues_WithSameListInstances_ReturnsTrue()
    {
        // Arrange - Use same list instances for both albums
        var sharedArtists = new List<string> { "Artist" };
        var sharedTracks = new List<TidalTrackInfo>();
        var sharedQualities = new List<TidalQuality> { TidalQuality.Lossless };

        var album1 = new TidalAlbumInfo(
            Id: "album-1",
            Title: "Album Title",
            Artists: sharedArtists,
            Tracks: sharedTracks,
            AvailableQualities: sharedQualities,
            ReleaseDate: new DateTime(2024, 1, 1),
            CoverArtId: "cover-1",
            IsAvailable: true,
            PrimaryArtistId: 100L
        );

        var album2 = new TidalAlbumInfo(
            Id: "album-1",
            Title: "Album Title",
            Artists: sharedArtists,
            Tracks: sharedTracks,
            AvailableQualities: sharedQualities,
            ReleaseDate: new DateTime(2024, 1, 1),
            CoverArtId: "cover-1",
            IsAvailable: true,
            PrimaryArtistId: 100L
        );

        // Act & Assert - Same list instances and simple values
        Assert.Equal(album1, album2);
    }

    [Fact]
    public void Equality_DifferentId_ReturnsFalse()
    {
        // Arrange
        var album1 = CreateTestAlbum("album-1");
        var album2 = CreateTestAlbum("album-2");

        // Act & Assert - Different Id values
        Assert.NotEqual(album1, album2);
    }

    [Fact]
    public void Equality_DifferentIsAvailable_ReturnsFalse()
    {
        // Arrange
        var album1 = new TidalAlbumInfo(
            Id: "album-1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true
        );

        var album2 = new TidalAlbumInfo(
            Id: "album-1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: false
        );

        // Act & Assert - Source line 16: bool IsAvailable differs
        Assert.NotEqual(album1, album2);
    }

    [Fact]
    public void Equality_DifferentPrimaryArtistId_ReturnsFalse()
    {
        // Arrange
        var album1 = new TidalAlbumInfo(
            Id: "album-1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true,
            PrimaryArtistId: 100L
        );

        var album2 = new TidalAlbumInfo(
            Id: "album-1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true,
            PrimaryArtistId: 200L
        );

        // Act & Assert - Source line 17: long? PrimaryArtistId differs
        Assert.NotEqual(album1, album2);
    }

    #endregion

    #region With Expression Tests (Record Immutability)

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new TidalAlbumInfo(
            Id: "original-id",
            Title: "Original Title",
            Artists: new List<string> { "Original Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.Low },
            ReleaseDate: new DateTime(2023, 1, 1),
            CoverArtId: "original-cover",
            IsAvailable: false,
            PrimaryArtistId: null
        );

        // Act - Create modified copy using with expression
        var modified = original with
        {
            Title = "Modified Title",
            IsAvailable = true,
            PrimaryArtistId = 999L
        };

        // Assert - Original unchanged, modified has new values
        Assert.Equal("Original Title", original.Title);
        Assert.False(original.IsAvailable);
        Assert.Null(original.PrimaryArtistId);

        Assert.Equal("original-id", modified.Id); // Unchanged
        Assert.Equal("Modified Title", modified.Title); // Changed
        Assert.True(modified.IsAvailable); // Changed
        Assert.Equal(999L, modified.PrimaryArtistId); // Changed
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ContainsPropertyNameAndValues()
    {
        // Arrange
        var album = new TidalAlbumInfo(
            Id: "test-id-789",
            Title: "Printable Album",
            Artists: new List<string> { "Test Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: new DateTime(2024, 6, 15),
            CoverArtId: "cover-xyz",
            IsAvailable: true
        );

        // Act
        var result = album.ToString();

        // Assert - Record ToString includes type name and property values
        Assert.Contains("TidalAlbumInfo", result);
        Assert.Contains("test-id-789", result);
        Assert.Contains("Printable Album", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_EmptyCollections_AreAllowed()
    {
        // Arrange & Act
        var album = new TidalAlbumInfo(
            Id: "empty-collections",
            Title: "Empty Album",
            Artists: new List<string>(),
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "",
            IsAvailable: false
        );

        // Assert - Empty collections are valid
        Assert.Empty(album.Artists);
        Assert.Empty(album.Tracks);
        Assert.Empty(album.AvailableQualities);
        Assert.Equal("", album.CoverArtId);
    }

    [Fact]
    public void Constructor_AllQualityValues_AreAccessible()
    {
        // Arrange
        var allQualities = new List<TidalQuality>
        {
            TidalQuality.Low,
            TidalQuality.High,
            TidalQuality.Lossless,
            TidalQuality.HiRes
        };

        // Act
        var album = new TidalAlbumInfo(
            Id: "all-qualities",
            Title: "HiFi Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: allQualities,
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true
        );

        // Assert - Source line 13: IReadOnlyList<TidalQuality> AvailableQualities
        Assert.Equal(4, album.AvailableQualities.Count);
        Assert.Equal(TidalQuality.Low, album.AvailableQualities[0]);
        Assert.Equal(TidalQuality.High, album.AvailableQualities[1]);
        Assert.Equal(TidalQuality.Lossless, album.AvailableQualities[2]);
        Assert.Equal(TidalQuality.HiRes, album.AvailableQualities[3]);
    }

    [Fact]
    public void Constructor_NullPrimaryArtistId_ExplicitNull_IsAllowed()
    {
        // Arrange & Act - Explicit null for optional parameter
        var album = new TidalAlbumInfo(
            Id: "explicit-null",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true,
            PrimaryArtistId: null
        );

        // Assert - Source line 17: long? PrimaryArtistId = null
        Assert.Null(album.PrimaryArtistId);
    }

    #endregion

    #region Helper Methods

    private static TidalAlbumInfo CreateTestAlbum(string id)
    {
        return new TidalAlbumInfo(
            Id: id,
            Title: "Test Album",
            Artists: new List<string> { "Test Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.High },
            ReleaseDate: DateTime.Today,
            CoverArtId: "test-cover",
            IsAvailable: true
        );
    }

    #endregion
}
