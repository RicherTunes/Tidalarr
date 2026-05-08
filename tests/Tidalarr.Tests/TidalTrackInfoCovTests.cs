using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalTrackInfo record.
/// Source: src/Tidalarr/Core/Models/TidalTrackInfo.cs
/// </summary>
public class TidalTrackInfoCovTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var expectedId = "track-123";
        var expectedTitle = "Test Track";
        var expectedArtists = new List<string> { "Artist One", "Artist Two" };
        var expectedAlbumId = "album-456";
        var expectedAlbumTitle = "Test Album";
        var expectedTrackNumber = 5;
        var expectedDuration = 240;
        var expectedQuality = TidalQuality.Lossless;
        var expectedIsAvailable = true;
        var expectedReleaseDate = new DateTime(2024, 3, 15);
        var expectedPrimaryArtistId = 12345L;

        // Act
        var track = new TidalTrackInfo(
            Id: expectedId,
            Title: expectedTitle,
            Artists: expectedArtists,
            AlbumId: expectedAlbumId,
            AlbumTitle: expectedAlbumTitle,
            TrackNumber: expectedTrackNumber,
            Duration: expectedDuration,
            Quality: expectedQuality,
            IsAvailable: expectedIsAvailable,
            ReleaseDate: expectedReleaseDate,
            PrimaryArtistId: expectedPrimaryArtistId
        );

        // Assert - Each property verified individually
        // Source line 9: string Id
        Assert.Equal(expectedId, track.Id);
        // Source line 10: string Title
        Assert.Equal(expectedTitle, track.Title);
        // Source line 11: IReadOnlyList<string> Artists
        Assert.Equal(2, track.Artists.Count);
        Assert.Equal("Artist One", track.Artists[0]);
        Assert.Equal("Artist Two", track.Artists[1]);
        // Source line 12: string AlbumId
        Assert.Equal(expectedAlbumId, track.AlbumId);
        // Source line 13: string AlbumTitle
        Assert.Equal(expectedAlbumTitle, track.AlbumTitle);
        // Source line 14: int TrackNumber
        Assert.Equal(expectedTrackNumber, track.TrackNumber);
        // Source line 15: int Duration
        Assert.Equal(expectedDuration, track.Duration);
        // Source line 16: TidalQuality Quality
        Assert.Equal(TidalQuality.Lossless, track.Quality);
        // Source line 17: bool IsAvailable
        Assert.True(track.IsAvailable);
        // Source line 18: DateTime ReleaseDate
        Assert.Equal(expectedReleaseDate, track.ReleaseDate);
        // Source line 19: long? PrimaryArtistId
        Assert.Equal(expectedPrimaryArtistId, track.PrimaryArtistId);
    }

    [Fact]
    public void Constructor_WithoutOptionalPrimaryArtistId_SetsNull()
    {
        // Arrange & Act
        var track = new TidalTrackInfo(
            Id: "track-no-artist-id",
            Title: "Track Without Artist ID",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: false,
            ReleaseDate: DateTime.Today
        );

        // Assert - Source line 19: long? PrimaryArtistId = null (default)
        Assert.Null(track.PrimaryArtistId);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equality_SameInstance_ReturnsTrue()
    {
        // Arrange - Create track with empty artists to avoid list reference comparison issues
        var track = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track Title",
            Artists: new List<string>(),
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 200,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: new DateTime(2024, 1, 1),
            PrimaryArtistId: 100L
        );

        // Act & Assert - Same instance equals itself
        Assert.Equal(track, track);
    }

    [Fact]
    public void Equality_SameSimpleValues_WithSameListInstances_ReturnsTrue()
    {
        // Arrange - Use same list instance for both tracks
        var sharedArtists = new List<string> { "Artist" };

        var track1 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track Title",
            Artists: sharedArtists,
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 200,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: new DateTime(2024, 1, 1),
            PrimaryArtistId: 100L
        );

        var track2 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track Title",
            Artists: sharedArtists,
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 200,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: new DateTime(2024, 1, 1),
            PrimaryArtistId: 100L
        );

        // Act & Assert - Same list instance and simple values
        Assert.Equal(track1, track2);
    }

    [Fact]
    public void Equality_DifferentId_ReturnsFalse()
    {
        // Arrange
        var track1 = CreateTestTrack("track-1");
        var track2 = CreateTestTrack("track-2");

        // Act & Assert - Different Id values
        Assert.NotEqual(track1, track2);
    }

    [Fact]
    public void Equality_DifferentTrackNumber_ReturnsFalse()
    {
        // Arrange
        var track1 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        var track2 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 2,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        // Act & Assert - Source line 14: int TrackNumber differs
        Assert.NotEqual(track1, track2);
    }

    [Fact]
    public void Equality_DifferentDuration_ReturnsFalse()
    {
        // Arrange
        var track1 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        var track2 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 200,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        // Act & Assert - Source line 15: int Duration differs
        Assert.NotEqual(track1, track2);
    }

    [Fact]
    public void Equality_DifferentQuality_ReturnsFalse()
    {
        // Arrange
        var track1 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        var track2 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        // Act & Assert - Source line 16: TidalQuality Quality differs
        Assert.NotEqual(track1, track2);
    }

    [Fact]
    public void Equality_DifferentIsAvailable_ReturnsFalse()
    {
        // Arrange
        var track1 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        var track2 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: false,
            ReleaseDate: DateTime.Today
        );

        // Act & Assert - Source line 17: bool IsAvailable differs
        Assert.NotEqual(track1, track2);
    }

    [Fact]
    public void Equality_DifferentPrimaryArtistId_ReturnsFalse()
    {
        // Arrange
        var track1 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today,
            PrimaryArtistId: 100L
        );

        var track2 = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today,
            PrimaryArtistId: 200L
        );

        // Act & Assert - Source line 19: long? PrimaryArtistId differs
        Assert.NotEqual(track1, track2);
    }

    #endregion

    #region With Expression Tests (Record Immutability)

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new TidalTrackInfo(
            Id: "original-id",
            Title: "Original Title",
            Artists: new List<string> { "Original Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Original Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.Low,
            IsAvailable: false,
            ReleaseDate: new DateTime(2023, 1, 1),
            PrimaryArtistId: null
        );

        // Act - Create modified copy using with expression
        var modified = original with
        {
            Title = "Modified Title",
            TrackNumber = 5,
            Duration = 240,
            Quality = TidalQuality.HiRes,
            IsAvailable = true,
            PrimaryArtistId = 999L
        };

        // Assert - Original unchanged, modified has new values
        Assert.Equal("Original Title", original.Title);
        Assert.Equal(1, original.TrackNumber);
        Assert.Equal(180, original.Duration);
        Assert.Equal(TidalQuality.Low, original.Quality);
        Assert.False(original.IsAvailable);
        Assert.Null(original.PrimaryArtistId);

        Assert.Equal("original-id", modified.Id); // Unchanged
        Assert.Equal("album-1", modified.AlbumId); // Unchanged
        Assert.Equal("Modified Title", modified.Title); // Changed
        Assert.Equal(5, modified.TrackNumber); // Changed
        Assert.Equal(240, modified.Duration); // Changed
        Assert.Equal(TidalQuality.HiRes, modified.Quality); // Changed
        Assert.True(modified.IsAvailable); // Changed
        Assert.Equal(999L, modified.PrimaryArtistId); // Changed
    }

    [Fact]
    public void With_Expression_NoChanges_CreatesEqualCopy()
    {
        // Arrange
        var original = new TidalTrackInfo(
            Id: "track-1",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today,
            PrimaryArtistId: 100L
        );

        // Act - Create copy with no changes
        var copy = original with { };

        // Assert - Same Id and values (but different instance)
        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Title, copy.Title);
        Assert.Equal(original.TrackNumber, copy.TrackNumber);
        Assert.Equal(original.PrimaryArtistId, copy.PrimaryArtistId);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ContainsPropertyNameAndValues()
    {
        // Arrange
        var track = new TidalTrackInfo(
            Id: "test-id-789",
            Title: "Printable Track",
            Artists: new List<string> { "Test Artist" },
            AlbumId: "album-456",
            AlbumTitle: "Printable Album",
            TrackNumber: 3,
            Duration: 210,
            Quality: TidalQuality.HiRes,
            IsAvailable: true,
            ReleaseDate: new DateTime(2024, 6, 15)
        );

        // Act
        var result = track.ToString();

        // Assert - Record ToString includes type name and property values
        Assert.Contains("TidalTrackInfo", result);
        Assert.Contains("test-id-789", result);
        Assert.Contains("Printable Track", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_EmptyArtists_AreAllowed()
    {
        // Arrange & Act
        var track = new TidalTrackInfo(
            Id: "empty-artists",
            Title: "Track Without Artists",
            Artists: new List<string>(),
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 0,
            Quality: TidalQuality.Low,
            IsAvailable: false,
            ReleaseDate: DateTime.MinValue
        );

        // Assert - Empty artists collection is valid
        Assert.Empty(track.Artists);
    }

    [Fact]
    public void Constructor_AllQualityValues_AreAccessible()
    {
        // Arrange & Act - Test all enum values
        var lowTrack = new TidalTrackInfo(
            Id: "low", Title: "Low", Artists: new List<string>(),
            AlbumId: "album", AlbumTitle: "Album", TrackNumber: 1, Duration: 180,
            Quality: TidalQuality.Low, IsAvailable: true, ReleaseDate: DateTime.Today
        );

        var highTrack = new TidalTrackInfo(
            Id: "high", Title: "High", Artists: new List<string>(),
            AlbumId: "album", AlbumTitle: "Album", TrackNumber: 1, Duration: 180,
            Quality: TidalQuality.High, IsAvailable: true, ReleaseDate: DateTime.Today
        );

        var losslessTrack = new TidalTrackInfo(
            Id: "lossless", Title: "Lossless", Artists: new List<string>(),
            AlbumId: "album", AlbumTitle: "Album", TrackNumber: 1, Duration: 180,
            Quality: TidalQuality.Lossless, IsAvailable: true, ReleaseDate: DateTime.Today
        );

        var hiResTrack = new TidalTrackInfo(
            Id: "hires", Title: "HiRes", Artists: new List<string>(),
            AlbumId: "album", AlbumTitle: "Album", TrackNumber: 1, Duration: 180,
            Quality: TidalQuality.HiRes, IsAvailable: true, ReleaseDate: DateTime.Today
        );

        // Assert - Source line 16: TidalQuality Quality - all enum values
        Assert.Equal(TidalQuality.Low, lowTrack.Quality);
        Assert.Equal(TidalQuality.High, highTrack.Quality);
        Assert.Equal(TidalQuality.Lossless, losslessTrack.Quality);
        Assert.Equal(TidalQuality.HiRes, hiResTrack.Quality);
    }

    [Fact]
    public void Constructor_NullPrimaryArtistId_ExplicitNull_IsAllowed()
    {
        // Arrange & Act - Explicit null for optional parameter
        var track = new TidalTrackInfo(
            Id: "explicit-null",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today,
            PrimaryArtistId: null
        );

        // Assert - Source line 19: long? PrimaryArtistId = null
        Assert.Null(track.PrimaryArtistId);
    }

    [Fact]
    public void Constructor_ZeroTrackNumber_IsAllowed()
    {
        // Arrange & Act
        var track = new TidalTrackInfo(
            Id: "zero-track",
            Title: "Track Zero",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 0,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        // Assert - Source line 14: int TrackNumber = 0
        Assert.Equal(0, track.TrackNumber);
    }

    [Fact]
    public void Constructor_ZeroDuration_IsAllowed()
    {
        // Arrange & Act
        var track = new TidalTrackInfo(
            Id: "zero-duration",
            Title: "Zero Duration Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 0,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        // Assert - Source line 15: int Duration = 0
        Assert.Equal(0, track.Duration);
    }

    [Fact]
    public void Constructor_EmptyStrings_AreAllowed()
    {
        // Arrange & Act
        var track = new TidalTrackInfo(
            Id: "",
            Title: "",
            Artists: new List<string>(),
            AlbumId: "",
            AlbumTitle: "",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );

        // Assert - Empty strings are valid
        Assert.Equal("", track.Id);
        Assert.Equal("", track.Title);
        Assert.Equal("", track.AlbumId);
        Assert.Equal("", track.AlbumTitle);
    }

    [Fact]
    public void Constructor_MinMaxDateTime_AreAllowed()
    {
        // Arrange & Act
        var minDateTrack = new TidalTrackInfo(
            Id: "min-date", Title: "Min Date", Artists: new List<string>(),
            AlbumId: "album", AlbumTitle: "Album", TrackNumber: 1, Duration: 180,
            Quality: TidalQuality.High, IsAvailable: true, ReleaseDate: DateTime.MinValue
        );

        var maxDateTrack = new TidalTrackInfo(
            Id: "max-date", Title: "Max Date", Artists: new List<string>(),
            AlbumId: "album", AlbumTitle: "Album", TrackNumber: 1, Duration: 180,
            Quality: TidalQuality.High, IsAvailable: true, ReleaseDate: DateTime.MaxValue
        );

        // Assert - Source line 18: DateTime ReleaseDate - min/max values
        Assert.Equal(DateTime.MinValue, minDateTrack.ReleaseDate);
        Assert.Equal(DateTime.MaxValue, maxDateTrack.ReleaseDate);
    }

    [Fact]
    public void Constructor_LargePrimaryArtistId_IsAllowed()
    {
        // Arrange & Act
        var track = new TidalTrackInfo(
            Id: "large-id",
            Title: "Track",
            Artists: new List<string> { "Artist" },
            AlbumId: "album-1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today,
            PrimaryArtistId: long.MaxValue
        );

        // Assert - Source line 19: long? PrimaryArtistId = long.MaxValue
        Assert.Equal(long.MaxValue, track.PrimaryArtistId);
    }

    #endregion

    #region Deconstruct Tests

    [Fact]
    public void Deconstruct_ReturnsAllPropertyValues()
    {
        // Arrange
        var expectedId = "deconstruct-test";
        var expectedTitle = "Deconstruct Track";
        var expectedArtists = new List<string> { "Artist" };
        var expectedAlbumId = "album-1";
        var expectedAlbumTitle = "Album";
        var expectedTrackNumber = 7;
        var expectedDuration = 300;
        var expectedQuality = TidalQuality.Lossless;
        var expectedIsAvailable = true;
        var expectedReleaseDate = new DateTime(2024, 12, 25);
        var expectedPrimaryArtistId = 98765L;

        var track = new TidalTrackInfo(
            Id: expectedId,
            Title: expectedTitle,
            Artists: expectedArtists,
            AlbumId: expectedAlbumId,
            AlbumTitle: expectedAlbumTitle,
            TrackNumber: expectedTrackNumber,
            Duration: expectedDuration,
            Quality: expectedQuality,
            IsAvailable: expectedIsAvailable,
            ReleaseDate: expectedReleaseDate,
            PrimaryArtistId: expectedPrimaryArtistId
        );

        // Act
        var (id, title, artists, albumId, albumTitle, trackNumber, duration, quality, isAvailable, releaseDate, primaryArtistId) = track;

        // Assert - All properties deconstructed correctly
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedTitle, title);
        Assert.Same(expectedArtists, artists);
        Assert.Equal(expectedAlbumId, albumId);
        Assert.Equal(expectedAlbumTitle, albumTitle);
        Assert.Equal(expectedTrackNumber, trackNumber);
        Assert.Equal(expectedDuration, duration);
        Assert.Equal(expectedQuality, quality);
        Assert.Equal(expectedIsAvailable, isAvailable);
        Assert.Equal(expectedReleaseDate, releaseDate);
        Assert.Equal(expectedPrimaryArtistId, primaryArtistId);
    }

    #endregion

    #region Helper Methods

    private static TidalTrackInfo CreateTestTrack(string id)
    {
        return new TidalTrackInfo(
            Id: id,
            Title: "Test Track",
            Artists: new List<string> { "Test Artist" },
            AlbumId: "test-album",
            AlbumTitle: "Test Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.Today
        );
    }

    #endregion
}
