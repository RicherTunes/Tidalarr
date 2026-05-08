using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalModelMapper focusing on edge cases not covered by existing tests.
/// </summary>
public class MapperCovTests
{
    private readonly TidalModelMapper _mapper = new();

    #region PrimaryArtistId Edge Cases

    [Fact]
    public void ToStreamingTrack_PrimaryArtistIdZero_FallsBackToArtistName()
    {
        // Arrange - Line 16: track.PrimaryArtistId > 0 - when 0, falls back to artist name
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist Name"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue,
            PrimaryArtistId: 0L); // Zero value - not null

        // Act
        StreamingTrack result = _mapper.ToStreamingTrack(track);

        // Assert - Line 18: 0 > 0 is false, so uses validArtists.FirstOrDefault()
        Assert.Equal("Artist Name", result.Artist.Id);
        Assert.Equal("Artist Name", result.Album.Artist.Id);
    }

    [Fact]
    public void ToStreamingTrack_PrimaryArtistIdNegative_FallsBackToArtistName()
    {
        // Arrange - Line 16: track.PrimaryArtistId > 0 - negative falls back
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist Name"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue,
            PrimaryArtistId: -1L); // Negative value

        // Act
        StreamingTrack result = _mapper.ToStreamingTrack(track);

        // Assert - Line 18: -1 > 0 is false, so uses validArtists.FirstOrDefault()
        Assert.Equal("Artist Name", result.Artist.Id);
    }

    [Fact]
    public void ToStreamingAlbum_PrimaryArtistIdZero_FallsBackToArtistName()
    {
        // Arrange - Line 70: album.PrimaryArtistId > 0 - when 0, falls back
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Album Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true,
            PrimaryArtistId: 0L); // Zero value

        // Act
        StreamingAlbum result = _mapper.ToStreamingAlbum(album);

        // Assert - Line 72: 0 > 0 is false, uses validArtists.FirstOrDefault()
        Assert.Equal("Album Artist", result.Artist.Id);
    }

    [Fact]
    public void ToStreamingAlbum_PrimaryArtistIdNegative_FallsBackToArtistName()
    {
        // Arrange - Line 70: album.PrimaryArtistId > 0 - negative falls back
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Album Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true,
            PrimaryArtistId: -999L); // Negative value

        // Act
        StreamingAlbum result = _mapper.ToStreamingAlbum(album);

        // Assert - Line 72: -999 > 0 is false
        Assert.Equal("Album Artist", result.Artist.Id);
    }

    #endregion

    #region Artist List Edge Cases

    [Fact]
    public void ToStreamingTrack_AllWhitespaceArtists_UsesUnknownArtist()
    {
        // Arrange - Line 13: Where(a => !string.IsNullOrWhiteSpace(a)) filters all out
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["  ", "\t", "\n"], // All whitespace
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue);

        // Act
        StreamingTrack result = _mapper.ToStreamingTrack(track);

        // Assert - Line 14: validArtists.Count = 0, uses UnknownArtist
        Assert.Equal("Unknown Artist", result.Artist.Name);
        Assert.Equal("Unknown Artist", result.Artist.Id);
    }

    [Fact]
    public void ToStreamingTrack_MultipleValidArtists_JoinsWithComma()
    {
        // Arrange - Line 14: string.Join(", ", validArtists)
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["First", "Second", "Third"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue);

        // Act
        StreamingTrack result = _mapper.ToStreamingTrack(track);

        // Assert - Line 14: joined with comma and space
        Assert.Equal("First, Second, Third", result.Artist.Name);
        Assert.Equal("First", result.Artist.Id); // First is primary
    }

    [Fact]
    public void ToStreamingAlbum_AllWhitespaceArtists_UsesUnknownArtist()
    {
        // Arrange - Line 67: Where(a => !string.IsNullOrWhiteSpace(a)) filters all
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["  ", "\t"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true);

        // Act
        StreamingAlbum result = _mapper.ToStreamingAlbum(album);

        // Assert - Line 68: validArtists.Count = 0
        Assert.Equal("Unknown Artist", result.Artist.Name);
        Assert.Equal("Unknown Artist", result.Artist.Id);
    }

    #endregion

    #region Metadata Edge Cases

    [Fact]
    public void ToStreamingTrack_MetadataContainsReleaseDate()
    {
        // Arrange - Line 53: ["release_date"] = track.ReleaseDate
        DateTime expectedDate = new(2024, 6, 15);
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: expectedDate);

        // Act
        StreamingTrack result = _mapper.ToStreamingTrack(track);

        // Assert - Line 53
        Assert.Equal(expectedDate, result.Metadata["release_date"]);
    }

    [Fact]
    public void ToStreamingTrack_MetadataContainsAlbumId()
    {
        // Arrange - Line 52: ["album_id"] = track.AlbumId
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist"],
            AlbumId: "album-xyz-123",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue);

        // Act
        StreamingTrack result = _mapper.ToStreamingTrack(track);

        // Assert - Line 52
        Assert.Equal("album-xyz-123", result.Metadata["album_id"]);
    }

    [Fact]
    public void ToStreamingAlbum_MetadataContainsIsAvailable()
    {
        // Arrange - Line 108: ["is_available"] = album.IsAvailable
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: false); // Not available

        // Act
        StreamingAlbum result = _mapper.ToStreamingAlbum(album);

        // Assert - Line 108
        Assert.Equal(false, result.Metadata["is_available"]);
    }

    [Fact]
    public void ToStreamingAlbum_MetadataContainsReleaseDate()
    {
        // Arrange - Line 109: ["release_date"] = album.ReleaseDate
        DateTime expectedDate = new(2023, 12, 25);
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: expectedDate,
            CoverArtId: "cover",
            IsAvailable: true);

        // Act
        StreamingAlbum result = _mapper.ToStreamingAlbum(album);

        // Assert - Line 109
        Assert.Equal(expectedDate, result.Metadata["release_date"]);
    }

    #endregion

    #region ToStreamingSearchResults Edge Cases

    [Fact]
    public void ToStreamingSearchResults_TrackWithNullId_UsesEmptyString()
    {
        // Arrange - Line 205: Id = track.Id ?? string.Empty
        TidalTrackInfo track = new(
            Id: null!,
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue);

        TidalSearchResults searchResults = new(
            Albums: [],
            Tracks: [track],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 205
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Id);
    }

    [Fact]
    public void ToStreamingSearchResults_TrackWithNullTitle_UsesEmptyString()
    {
        // Arrange - Line 206: Title = track.Title ?? string.Empty
        TidalTrackInfo track = new(
            Id: "t1",
            Title: null!,
            Artists: ["Artist"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue);

        TidalSearchResults searchResults = new(
            Albums: [],
            Tracks: [track],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 206
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Title);
    }

    [Fact]
    public void ToStreamingSearchResults_AlbumWithNullId_UsesEmptyString()
    {
        // Arrange - Line 180: Id = album.Id ?? string.Empty
        TidalAlbumInfo album = new(
            Id: null!,
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 180
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Id);
    }

    [Fact]
    public void ToStreamingSearchResults_AlbumWithNullTitle_UsesEmptyString()
    {
        // Arrange - Line 181: Title = album.Title ?? string.Empty
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: null!,
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 181
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Title);
    }

    [Fact]
    public void ToStreamingSearchResults_AlbumMetadataTidalType()
    {
        // Arrange - Line 194: ["tidal_type"] = "album"
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 194
        Assert.Single(result);
        Assert.Equal("album", result[0].Metadata["tidal_type"]);
    }

    [Fact]
    public void ToStreamingSearchResults_TrackMetadataTidalType()
    {
        // Arrange - Line 217: ["tidal_type"] = "track"
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue);

        TidalSearchResults searchResults = new(
            Albums: [],
            Tracks: [track],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 217
        Assert.Single(result);
        Assert.Equal("track", result[0].Metadata["tidal_type"]);
    }

    [Fact]
    public void ToStreamingSearchResults_TrackWithZeroDuration_ReturnsZeroTimeSpan()
    {
        // Arrange - Line 213: Duration = TimeSpan.FromSeconds(track.Duration)
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 0,
            Quality: TidalQuality.High,
            IsAvailable: true,
            ReleaseDate: DateTime.MinValue);

        TidalSearchResults searchResults = new(
            Albums: [],
            Tracks: [track],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 213
        Assert.Single(result);
        Assert.Equal(TimeSpan.Zero, result[0].Duration);
    }

    #endregion

    #region ToStreamingQuality Default Case

    [Fact]
    public void ToStreamingQuality_InvalidEnumValue_ReturnsDefault()
    {
        // Arrange - Line 143: _ => new StreamingQuality { Id = "HIGH", ... }
        TidalQuality invalidQuality = (TidalQuality)999;

        // Act
        StreamingQuality result = _mapper.ToStreamingQuality(invalidQuality);

        // Assert - Line 143: default case returns HIGH
        Assert.Equal("HIGH", result.Id);
        Assert.Equal("High", result.Name);
        Assert.Equal("AAC", result.Format);
        Assert.Equal(320, result.Bitrate);
        Assert.Equal(44100, result.SampleRate);
    }

    #endregion
}
