using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

/// <summary>
/// Edge case coverage tests for TidalModelMapper - covers paths not in MapperCovTests.
/// Target: src/Tidalarr/Core/Mappers/TidalModelMapper.cs
/// </summary>
public class ModelMapperEdgeCovTests
{
    private readonly TidalModelMapper _mapper = new();

    #region ToStreamingArtist - Lines 119-132

    [Fact]
    public void ToStreamingArtist_WithValidParameters_MapsCorrectly()
    {
        // Arrange - Line 119: ToStreamingArtist(string artistId, string artistName, Dictionary<string, object>? metadata = null)
        // Act
        StreamingArtist result = _mapper.ToStreamingArtist("artist-123", "Test Artist");

        // Assert - Lines 121-131
        Assert.Equal("artist-123", result.Id);
        Assert.Equal("Test Artist", result.Name);
        Assert.Equal(string.Empty, result.Biography);
        Assert.Empty(result.Genres);
        Assert.Equal(string.Empty, result.Country);
        Assert.Empty(result.ImageUrls);
        Assert.Empty(result.ExternalUrls);
        Assert.Empty(result.Metadata);
    }

    [Fact]
    public void ToStreamingArtist_WithNullArtistId_UsesEmptyString()
    {
        // Arrange - Line 123: Id = artistId ?? string.Empty
        // Act
        StreamingArtist result = _mapper.ToStreamingArtist(null, "Artist Name");

        // Assert - Line 123
        Assert.Equal(string.Empty, result.Id);
        Assert.Equal("Artist Name", result.Name);
    }

    [Fact]
    public void ToStreamingArtist_WithNullArtistName_UsesEmptyString()
    {
        // Arrange - Line 124: Name = artistName ?? string.Empty
        // Act
        StreamingArtist result = _mapper.ToStreamingArtist("artist-1", null);

        // Assert - Line 124
        Assert.Equal("artist-1", result.Id);
        Assert.Equal(string.Empty, result.Name);
    }

    [Fact]
    public void ToStreamingArtist_WithMetadata_PassesThrough()
    {
        // Arrange - Line 130: Metadata = metadata ?? []
        Dictionary<string, object> metadata = new()
        {
            ["custom_key"] = "custom_value",
            ["number"] = 42
        };

        // Act
        StreamingArtist result = _mapper.ToStreamingArtist("artist-1", "Artist", metadata);

        // Assert - Line 130
        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal("custom_value", result.Metadata["custom_key"]);
        Assert.Equal(42, result.Metadata["number"]);
    }

    [Fact]
    public void ToStreamingArtist_WithNullMetadata_UsesEmptyDictionary()
    {
        // Arrange - Line 130: metadata ?? []
        // Act
        StreamingArtist result = _mapper.ToStreamingArtist("artist-1", "Artist", null);

        // Assert - Line 130
        Assert.Empty(result.Metadata);
    }

    #endregion

    #region FromStreamingQuality - Lines 147-159

    [Fact]
    public void FromStreamingQuality_LowTier_ReturnsLow()
    {
        // Arrange - Line 152: StreamingQualityTier.Low => TidalQuality.Low
        StreamingQuality quality = new()
        {
            Bitrate = 96,
            Format = "AAC",
            SampleRate = 44100
        };

        // Act
        TidalQuality result = _mapper.FromStreamingQuality(quality);

        // Assert - Line 152
        Assert.Equal(TidalQuality.Low, result);
    }

    [Fact]
    public void FromStreamingQuality_NormalTier_ReturnsHigh()
    {
        // Arrange - Line 153: StreamingQualityTier.Normal => TidalQuality.High
        StreamingQuality quality = new()
        {
            Bitrate = 160,
            Format = "AAC",
            SampleRate = 44100
        };

        // Act
        TidalQuality result = _mapper.FromStreamingQuality(quality);

        // Assert - Line 153
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void FromStreamingQuality_HighTier_ReturnsHigh()
    {
        // Arrange - Line 154: StreamingQualityTier.High => TidalQuality.High
        StreamingQuality quality = new()
        {
            Bitrate = 320,
            Format = "AAC",
            SampleRate = 44100
        };

        // Act
        TidalQuality result = _mapper.FromStreamingQuality(quality);

        // Assert - Line 154
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void FromStreamingQuality_LosslessTier_ReturnsLossless()
    {
        // Arrange - Line 155: StreamingQualityTier.Lossless => TidalQuality.Lossless
        StreamingQuality quality = new()
        {
            Format = "FLAC",
            BitDepth = 16,
            SampleRate = 44100
        };

        // Act
        TidalQuality result = _mapper.FromStreamingQuality(quality);

        // Assert - Line 155
        Assert.Equal(TidalQuality.Lossless, result);
    }

    [Fact]
    public void FromStreamingQuality_HiResTier_ReturnsHiRes()
    {
        // Arrange - Line 156: StreamingQualityTier.HiRes => TidalQuality.HiRes
        StreamingQuality quality = new()
        {
            Format = "FLAC",
            BitDepth = 24,
            SampleRate = 96000
        };

        // Act
        TidalQuality result = _mapper.FromStreamingQuality(quality);

        // Assert - Line 156
        Assert.Equal(TidalQuality.HiRes, result);
    }

    [Fact]
    public void FromStreamingQuality_LowBitrate_ReturnsHigh()
    {
        // Arrange - Line 157: _ => TidalQuality.High (default case)
        // Using low bitrate (below 160) will return Low tier, but we want to test
        // that Normal tier also maps to High. Bitrate 160-319 returns Normal tier.
        StreamingQuality quality = new()
        {
            Format = "AAC",
            Bitrate = 192,
            SampleRate = 44100
        };

        // Act
        TidalQuality result = _mapper.FromStreamingQuality(quality);

        // Assert - Line 153: Normal tier => TidalQuality.High
        Assert.Equal(TidalQuality.High, result);
    }

    #endregion

    #region ToStreamingTracks - Lines 161-170

    [Fact]
    public void ToStreamingTracks_WithAlbumAndTracks_MapsAllTracks()
    {
        // Arrange - Lines 163-169
        TidalAlbumInfo album = new(
            Id: "album-1",
            Title: "Test Album",
            Artists: ["Album Artist"],
            Tracks:
            [
                new TidalTrackInfo("t1", "Song 1", ["Artist"], "album-1", "Test Album", 1, 180, TidalQuality.High, true, DateTime.MinValue),
                new TidalTrackInfo("t2", "Song 2", ["Artist"], "album-1", "Test Album", 2, 200, TidalQuality.Lossless, true, DateTime.MinValue)
            ],
            AvailableQualities: [TidalQuality.High, TidalQuality.Lossless],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover-1",
            IsAvailable: true);

        // Act - Line 163
        List<StreamingTrack> result = _mapper.ToStreamingTracks(album);

        // Assert - Lines 164-169
        Assert.Equal(2, result.Count);
        Assert.Equal("t1", result[0].Id);
        Assert.Equal("t2", result[1].Id);
        // Both tracks share same album instance - Line 167
        Assert.Same(result[0].Album, result[1].Album);
        Assert.Equal("album-1", result[0].Album.Id);
        Assert.Equal("Test Album", result[0].Album.Title);
    }

    [Fact]
    public void ToStreamingTracks_NullTracks_ReturnsEmptyList()
    {
        // Arrange - Line 164: (album.Tracks ?? [])
        TidalAlbumInfo album = new(
            Id: "album-1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: null!,
            AvailableQualities: [],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true);

        // Act
        List<StreamingTrack> result = _mapper.ToStreamingTracks(album);

        // Assert - Line 164: null coalesces to empty list
        Assert.Empty(result);
    }

    [Fact]
    public void ToStreamingTracks_EmptyTracks_ReturnsEmptyList()
    {
        // Arrange - Line 164: (album.Tracks ?? [])
        TidalAlbumInfo album = new(
            Id: "album-1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true);

        // Act
        List<StreamingTrack> result = _mapper.ToStreamingTracks(album);

        // Assert - Line 164
        Assert.Empty(result);
    }

    [Fact]
    public void ToStreamingTracks_SetsAlbumOnEachTrack()
    {
        // Arrange - Lines 166-168: streamingTrack.Album = streamingAlbum
        TidalAlbumInfo album = new(
            Id: "album-xyz",
            Title: "My Album",
            Artists: ["Album Artist"],
            Tracks: [new TidalTrackInfo("t1", "Song", ["Track Artist"], "album-xyz", "My Album", 1, 180, TidalQuality.High, true, DateTime.MinValue)],
            AvailableQualities: [],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "cover",
            IsAvailable: true);

        // Act
        List<StreamingTrack> result = _mapper.ToStreamingTracks(album);

        // Assert - Line 167: Album property is set to the streaming album
        Assert.Single(result);
        Assert.Equal("album-xyz", result[0].Album.Id);
        Assert.Equal("My Album", result[0].Album.Title);
        Assert.Equal("Album Artist", result[0].Album.Artist.Name);
    }

    #endregion

    #region ToStreamingAlbum - CoverArtUrls (download-path artwork embedding)

    [Fact]
    public void ToStreamingAlbum_BuildsRealTidalCoverArtUrls_NotRawId()
    {
        TidalAlbumInfo album = new(
            Id: "album-1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "1234-5678-90ab",
            IsAvailable: true);

        StreamingAlbum result = _mapper.ToStreamingAlbum(album);

        // Real resources.tidal.com image URL (dashes -> path slashes), not the raw id — Common's
        // orchestrator embeds GetBestCoverArtUrl() and a raw id can't be fetched.
        Assert.Equal("https://resources.tidal.com/images/1234/5678/90ab/1280x1280.jpg", result.CoverArtUrls["large"]);
        Assert.StartsWith("https://resources.tidal.com/images/", result.GetBestCoverArtUrl());
        Assert.DoesNotContain("1234-5678-90ab", result.GetBestCoverArtUrl());
    }

    [Fact]
    public void ToStreamingAlbum_EmptyCoverArtId_YieldsNoCoverUrls()
    {
        TidalAlbumInfo album = new(
            Id: "album-1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "",
            IsAvailable: true);

        StreamingAlbum result = _mapper.ToStreamingAlbum(album);

        Assert.Empty(result.CoverArtUrls);
    }

    #endregion

    #region ToStreamingQuality - Lines 134-145 (default case)

    [Fact]
    public void ToStreamingQuality_LowQuality_MapsCorrectly()
    {
        // Arrange - Line 139: TidalQuality.Low
        // Act
        StreamingQuality result = _mapper.ToStreamingQuality(TidalQuality.Low);

        // Assert - Line 139
        Assert.Equal("LOW", result.Id);
        Assert.Equal("Low", result.Name);
        Assert.Equal("AAC", result.Format);
        Assert.Equal(96, result.Bitrate);
        Assert.Equal(44100, result.SampleRate);
    }

    [Fact]
    public void ToStreamingQuality_HighQuality_MapsCorrectly()
    {
        // Arrange - Line 140: TidalQuality.High
        // Act
        StreamingQuality result = _mapper.ToStreamingQuality(TidalQuality.High);

        // Assert - Line 140
        Assert.Equal("HIGH", result.Id);
        Assert.Equal("High", result.Name);
        Assert.Equal("AAC", result.Format);
        Assert.Equal(320, result.Bitrate);
        Assert.Equal(44100, result.SampleRate);
    }

    [Fact]
    public void ToStreamingQuality_LosslessQuality_MapsCorrectly()
    {
        // Arrange - Line 141: TidalQuality.Lossless
        // Act
        StreamingQuality result = _mapper.ToStreamingQuality(TidalQuality.Lossless);

        // Assert - Line 141
        Assert.Equal("LOSSLESS", result.Id);
        Assert.Equal("Lossless", result.Name);
        Assert.Equal("FLAC", result.Format);
        Assert.Equal(16, result.BitDepth);
        Assert.Equal(44100, result.SampleRate);
    }

    [Fact]
    public void ToStreamingQuality_HiResQuality_MapsCorrectly()
    {
        // Arrange - Line 142: TidalQuality.HiRes
        // Act
        StreamingQuality result = _mapper.ToStreamingQuality(TidalQuality.HiRes);

        // Assert - Line 142
        Assert.Equal("HI_RES", result.Id);
        Assert.Equal("Master", result.Name);
        Assert.Equal("FLAC", result.Format);
        Assert.Equal(24, result.BitDepth);
        Assert.Equal(96000, result.SampleRate);
    }

    [Fact]
    public void ToStreamingQuality_UnknownQuality_ReturnsHighDefault()
    {
        // Arrange - Line 143: _ => new StreamingQuality { Id = "HIGH", ... }
        TidalQuality unknownQuality = (TidalQuality)999;

        // Act
        StreamingQuality result = _mapper.ToStreamingQuality(unknownQuality);

        // Assert - Line 143: default case
        Assert.Equal("HIGH", result.Id);
        Assert.Equal("High", result.Name);
        Assert.Equal("AAC", result.Format);
        Assert.Equal(320, result.Bitrate);
        Assert.Equal(44100, result.SampleRate);
    }

    #endregion

    #region ToStreamingSearchResults - Lines 172-225

    [Fact]
    public void ToStreamingSearchResults_NullResults_ReturnsEmptyList()
    {
        // Arrange - Line 176: if (searchResults?.Albums != null)
        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(null);

        // Assert - Line 176: null searchResults returns empty
        Assert.Empty(result);
    }

    [Fact]
    public void ToStreamingSearchResults_WithAlbums_MapsToSearchResults()
    {
        // Arrange - Lines 176-199
        TidalAlbumInfo album = new(
            Id: "album-search-1",
            Title: "Search Album",
            Artists: ["Search Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.HiRes],
            ReleaseDate: new DateTime(2024, 3, 15),
            CoverArtId: "search-cover",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Lines 178-198
        Assert.Single(result);
        Assert.Equal("album-search-1", result[0].Id);
        Assert.Equal("Search Album", result[0].Title);
        Assert.Equal("Search Artist", result[0].Artist);
        Assert.Equal(StreamingSearchType.Album, result[0].Type);
        Assert.Equal(new DateTime(2024, 3, 15), result[0].ReleaseDate);
    }

    [Fact]
    public void ToStreamingSearchResults_WithTracks_MapsToSearchResults()
    {
        // Arrange - Lines 201-222
        TidalTrackInfo track = new(
            Id: "track-search-1",
            Title: "Search Track",
            Artists: ["Track Artist"],
            AlbumId: "album-1",
            AlbumTitle: "Track Album",
            TrackNumber: 1,
            Duration: 240,
            Quality: TidalQuality.HiRes,
            IsAvailable: true,
            ReleaseDate: new DateTime(2024, 5, 20));

        TidalSearchResults searchResults = new(
            Albums: [],
            Tracks: [track],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Lines 203-221
        Assert.Single(result);
        Assert.Equal("track-search-1", result[0].Id);
        Assert.Equal("Search Track", result[0].Title);
        Assert.Equal("Track Artist", result[0].Artist);
        Assert.Equal("Track Album", result[0].Album);
        Assert.Equal(StreamingSearchType.Track, result[0].Type);
        Assert.Equal(new DateTime(2024, 5, 20), result[0].ReleaseDate);
        Assert.Equal(TimeSpan.FromSeconds(240), result[0].Duration);
    }

    [Fact]
    public void ToStreamingSearchResults_AlbumWithCoverArt_GeneratesUrl()
    {
        // Arrange - Line 188: CoverArtUrl with hyphen replacement
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "abc-def-123-456",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 188: hyphens replaced with slashes
        Assert.Single(result);
        Assert.Equal("https://resources.tidal.com/images/abc/def/123/456/320x320.jpg", result[0].CoverArtUrl);
    }

    [Fact]
    public void ToStreamingSearchResults_AlbumWithNullArtists_JoinsEmpty()
    {
        // Arrange - Line 182: string.Join(", ", album.Artists ?? [])
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: null!,
            Tracks: [],
            AvailableQualities: [],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 182: null artists joins to empty string
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Artist);
    }

    [Fact]
    public void ToStreamingSearchResults_TrackWithNullArtists_JoinsEmpty()
    {
        // Arrange - Line 207: string.Join(", ", track.Artists ?? [])
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Track",
            Artists: null!,
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

        // Assert - Line 207
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Artist);
    }

    [Fact]
    public void ToStreamingSearchResults_TrackMetadata_ContainsQualityAndIsHires()
    {
        // Arrange - Lines 218-219
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Track",
            Artists: ["Artist"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 180,
            Quality: TidalQuality.HiRes,
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

        // Assert - Lines 218-219
        Assert.Single(result);
        Assert.Equal("HiRes", result[0].Metadata["quality"]);
        Assert.Equal(true, result[0].Metadata["is_hires"]);
    }

    [Fact]
    public void ToStreamingSearchResults_AlbumMetadata_ContainsIsHires()
    {
        // Arrange - Line 196: is_hires based on HiRes presence
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High, TidalQuality.HiRes],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 196: HiRes in qualities means is_hires = true
        Assert.Single(result);
        Assert.Equal(true, result[0].Metadata["is_hires"]);
    }

    [Fact]
    public void ToStreamingSearchResults_AlbumWithoutHiRes_IsHiresFalse()
    {
        // Arrange - Line 196: is_hires = false when HiRes not present
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Low, TidalQuality.High],
            ReleaseDate: DateTime.MinValue,
            CoverArtId: "",
            IsAvailable: true);

        TidalSearchResults searchResults = new(
            Albums: [album],
            Tracks: [],
            Artists: [],
            TotalCount: 1,
            HasMore: false);

        // Act
        List<StreamingSearchResult> result = _mapper.ToStreamingSearchResults(searchResults);

        // Assert - Line 196
        Assert.Single(result);
        Assert.Equal(false, result[0].Metadata["is_hires"]);
    }

    #endregion
}
