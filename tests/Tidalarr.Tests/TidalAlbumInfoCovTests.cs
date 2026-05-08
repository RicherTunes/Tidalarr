using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalAlbumInfo record - complementary coverage.
/// Source: src/Tidalarr/Core/Models/TidalAlbumInfo.cs
/// </summary>
public class TidalAlbumInfoCovTests
{
    #region Deconstruction Tests

    [Fact]
    public void Deconstruct_ReturnsAllPropertyValues()
    {
        // Arrange - Source lines 9-17: record positional parameters
        var expectedId = "deconstruct-album-id";
        var expectedTitle = "Deconstruct Test Album";
        var expectedArtists = new List<string> { "Artist A", "Artist B" };
        var expectedTracks = new List<TidalTrackInfo>
        {
            new(
                Id: "track-decon-1",
                Title: "Decon Track",
                Artists: new List<string> { "Artist A" },
                AlbumId: "deconstruct-album-id",
                AlbumTitle: "Deconstruct Test Album",
                TrackNumber: 1,
                Duration: 240,
                Quality: TidalQuality.Lossless,
                IsAvailable: true,
                ReleaseDate: new DateTime(2024, 1, 1)
            )
        };
        var expectedQualities = new List<TidalQuality> { TidalQuality.Lossless, TidalQuality.HiRes };
        var expectedReleaseDate = new DateTime(2024, 5, 20);
        var expectedCoverArtId = "decon-cover-uuid";
        var expectedIsAvailable = true;
        var expectedPrimaryArtistId = 98765L;

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

        // Act - Deconstruct the record
        var (id, title, artists, tracks, availableQualities, releaseDate, coverArtId, isAvailable, primaryArtistId) = album;

        // Assert - Each deconstructed value matches original
        // Source line 9: string Id
        Assert.Equal(expectedId, id);
        // Source line 10: string Title
        Assert.Equal(expectedTitle, title);
        // Source line 11: IReadOnlyList<string> Artists
        Assert.Same(expectedArtists, artists);
        // Source line 12: IReadOnlyList<TidalTrackInfo> Tracks
        Assert.Same(expectedTracks, tracks);
        // Source line 13: IReadOnlyList<TidalQuality> AvailableQualities
        Assert.Same(expectedQualities, availableQualities);
        // Source line 14: DateTime ReleaseDate
        Assert.Equal(expectedReleaseDate, releaseDate);
        // Source line 15: string CoverArtId
        Assert.Equal(expectedCoverArtId, coverArtId);
        // Source line 16: bool IsAvailable
        Assert.Equal(expectedIsAvailable, isAvailable);
        // Source line 17: long? PrimaryArtistId
        Assert.Equal(expectedPrimaryArtistId, primaryArtistId);
    }

    [Fact]
    public void Deconstruct_WithNullPrimaryArtistId_ReturnsNull()
    {
        // Arrange - Source line 17: long? PrimaryArtistId = null
        var album = new TidalAlbumInfo(
            Id: "decon-null-id",
            Title: "Null Artist ID Album",
            Artists: new List<string> { "Solo Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.High },
            ReleaseDate: new DateTime(2023, 12, 1),
            CoverArtId: "null-cover",
            IsAvailable: false
        );

        // Act
        var (_, _, _, _, _, _, _, _, primaryArtistId) = album;

        // Assert - Optional parameter defaults to null
        Assert.Null(primaryArtistId);
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_SameInstance_ReturnsSameValue()
    {
        // Arrange
        var album = CreateTestAlbum("hash-test-1");

        // Act
        var hash1 = album.GetHashCode();
        var hash2 = album.GetHashCode();

        // Assert - GetHashCode is consistent for same instance
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_SameValuesWithSameListInstances_ReturnsSameHash()
    {
        // Arrange - Use same list instances
        var sharedArtists = new List<string> { "Shared Artist" };
        var sharedTracks = new List<TidalTrackInfo>();
        var sharedQualities = new List<TidalQuality> { TidalQuality.High };

        var album1 = new TidalAlbumInfo(
            Id: "hash-same",
            Title: "Hash Test",
            Artists: sharedArtists,
            Tracks: sharedTracks,
            AvailableQualities: sharedQualities,
            ReleaseDate: new DateTime(2024, 1, 1),
            CoverArtId: "hash-cover",
            IsAvailable: true,
            PrimaryArtistId: 100L
        );

        var album2 = new TidalAlbumInfo(
            Id: "hash-same",
            Title: "Hash Test",
            Artists: sharedArtists,
            Tracks: sharedTracks,
            AvailableQualities: sharedQualities,
            ReleaseDate: new DateTime(2024, 1, 1),
            CoverArtId: "hash-cover",
            IsAvailable: true,
            PrimaryArtistId: 100L
        );

        // Act & Assert - Equal objects have equal hash codes
        Assert.Equal(album1.GetHashCode(), album2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentId_ReturnsDifferentHash()
    {
        // Arrange
        var album1 = CreateTestAlbum("hash-diff-1");
        var album2 = CreateTestAlbum("hash-diff-2");

        // Act & Assert - Different Id typically produces different hash
        Assert.NotEqual(album1.GetHashCode(), album2.GetHashCode());
    }

    #endregion

    #region Additional Equality Tests

    [Fact]
    public void Equality_DifferentTitle_ReturnsFalse()
    {
        // Arrange - Source line 10: string Title
        var album1 = new TidalAlbumInfo(
            Id: "same-id",
            Title: "First Title",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true
        );

        var album2 = new TidalAlbumInfo(
            Id: "same-id",
            Title: "Different Title",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true
        );

        // Act & Assert - Different Title values
        Assert.NotEqual(album1, album2);
    }

    [Fact]
    public void Equality_DifferentReleaseDate_ReturnsFalse()
    {
        // Arrange - Source line 14: DateTime ReleaseDate
        var album1 = new TidalAlbumInfo(
            Id: "same-id",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: new DateTime(2024, 1, 1),
            CoverArtId: "cover",
            IsAvailable: true
        );

        var album2 = new TidalAlbumInfo(
            Id: "same-id",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: new DateTime(2024, 12, 31),
            CoverArtId: "cover",
            IsAvailable: true
        );

        // Act & Assert - Different ReleaseDate values
        Assert.NotEqual(album1, album2);
    }

    [Fact]
    public void Equality_DifferentCoverArtId_ReturnsFalse()
    {
        // Arrange - Source line 15: string CoverArtId
        var album1 = new TidalAlbumInfo(
            Id: "same-id",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover-1",
            IsAvailable: true
        );

        var album2 = new TidalAlbumInfo(
            Id: "same-id",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover-2",
            IsAvailable: true
        );

        // Act & Assert - Different CoverArtId values
        Assert.NotEqual(album1, album2);
    }

    [Fact]
    public void Equality_BothNullPrimaryArtistId_ReturnsTrue()
    {
        // Arrange - Source line 17: long? PrimaryArtistId = null (both omitted).
        // Records compare collection-typed properties by reference, so share the same list
        // instances between both records — otherwise the equality check fails on Artists/Tracks/Qualities
        // even though every scalar (including PrimaryArtistId) matches.
        var artists = new List<string> { "Artist" };
        var tracks = new List<TidalTrackInfo>();
        var qualities = new List<TidalQuality>();
        var releaseDate = DateTime.Today;

        var album1 = new TidalAlbumInfo(
            Id: "null-compare",
            Title: "Album",
            Artists: artists,
            Tracks: tracks,
            AvailableQualities: qualities,
            ReleaseDate: releaseDate,
            CoverArtId: "cover",
            IsAvailable: true
        );

        var album2 = new TidalAlbumInfo(
            Id: "null-compare",
            Title: "Album",
            Artists: artists,
            Tracks: tracks,
            AvailableQualities: qualities,
            ReleaseDate: releaseDate,
            CoverArtId: "cover",
            IsAvailable: true
        );

        // Act & Assert - Both have null PrimaryArtistId
        Assert.Equal(album1, album2);
    }

    [Fact]
    public void Equality_NullVsValuePrimaryArtistId_ReturnsFalse()
    {
        // Arrange - Source line 17: long? PrimaryArtistId comparison
        var albumWithNull = new TidalAlbumInfo(
            Id: "null-vs-value",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true
        );

        var albumWithValue = new TidalAlbumInfo(
            Id: "null-vs-value",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: DateTime.Today,
            CoverArtId: "cover",
            IsAvailable: true,
            PrimaryArtistId: 12345L
        );

        // Act & Assert - null != 12345L
        Assert.NotEqual(albumWithNull, albumWithValue);
    }

    #endregion

    #region Record Copy Tests

    [Fact]
    public void With_Expression_NoChanges_CreatesEqualCopy()
    {
        // Arrange
        var original = new TidalAlbumInfo(
            Id: "copy-test-id",
            Title: "Original Title",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.High },
            ReleaseDate: new DateTime(2024, 3, 15),
            CoverArtId: "original-cover",
            IsAvailable: true,
            PrimaryArtistId: 500L
        );

        // Act - With expression with no changes
        var copy = original with { };

        // Assert - Copy is equal but not same instance
        Assert.Equal(original, copy);
        Assert.NotSame(original, copy);
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
