using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalModelMapperTests
{
    private readonly TidalModelMapper _mapper = new();

    [Fact]
    public void ToStreamingTrack_MapsCoreFields_AndMetadata()
    {
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist A", "Artist B"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 3,
            Duration: 200,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: new DateTime(2020, 1, 2));

        StreamingTrack st = this._mapper.ToStreamingTrack(track);
        Assert.Equal("t1", st.Id);
        Assert.Equal("Song", st.Title);
        Assert.Equal("Artist A, Artist B", st.Artist.Name);
        Assert.Equal("al1", st.Album.Id);
        Assert.Equal(TimeSpan.FromSeconds(200), st.Duration);
        Assert.Contains(st.AvailableQualities, q => q.Id == "LOSSLESS");
        Assert.Equal("t1", st.Metadata["tidal_id"]);
        Assert.Equal("al1", st.Metadata["album_id"]);
        Assert.Equal(true, st.Metadata["is_available"]);
    }

    [Fact]
    public void ToStreamingTrack_MapsIsrc_ForLidarrImportAnchoring()
    {
        // ISRC anchors Lidarr's import track-matching far above fuzzy title distance.
        // Regression guard: the mapper previously hardcoded Isrc = string.Empty, dropping the
        // ISRC Tidal returns (TidalTrackDto.isrc) and weakening import matching ("Worst track match").
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist A"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 200,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: new DateTime(2020, 1, 2))
        {
            Isrc = "USABC1234567"
        };

        StreamingTrack st = this._mapper.ToStreamingTrack(track);

        Assert.Equal("USABC1234567", st.Isrc);
    }

    [Fact]
    public void ToStreamingTrack_WithPrimaryArtistId_UsesArtistId_NotName()
    {
        // Arrange
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist A"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 200,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: new DateTime(2020, 1, 2),
            PrimaryArtistId: 123456L);

        // Act
        StreamingTrack st = this._mapper.ToStreamingTrack(track);

        // Assert - Artist ID should be used, not name
        Assert.Equal("123456", st.Artist.Id);
        Assert.Equal("123456", st.Album.Artist.Id);
        Assert.Equal("Artist A", st.Artist.Name);
    }

    [Fact]
    public void ToStreamingTrack_WithoutPrimaryArtistId_FallsBackToArtistName()
    {
        // Arrange
        TidalTrackInfo track = new(
            Id: "t1",
            Title: "Song",
            Artists: ["Artist A"],
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 1,
            Duration: 200,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: new DateTime(2020, 1, 2));

        // Act
        StreamingTrack st = this._mapper.ToStreamingTrack(track);

        // Assert - Artist name should be used as ID when PrimaryArtistId is null
        Assert.Equal("Artist A", st.Artist.Id);
        Assert.Equal("Artist A", st.Album.Artist.Id);
        Assert.Equal("Artist A", st.Artist.Name);
    }

    [Fact]
    public void ToStreamingAlbum_MapsQualities_CoverUrls_AndMetadata()
    {
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist A"],
            Tracks: [],
            AvailableQualities: [TidalQuality.High, TidalQuality.HiRes],
            ReleaseDate: new DateTime(2020, 1, 3),
            CoverArtId: "aa-bb-cc",
            IsAvailable: true);

        StreamingAlbum sa = this._mapper.ToStreamingAlbum(album);
        Assert.Equal("al1", sa.Id);
        Assert.Contains(sa.AvailableQualities, q => q.Id == "HIGH");
        Assert.Contains(sa.AvailableQualities, q => q.Id == "HI_RES");
        // CoverArtId is a Tidal resource id (dash-separated), not a URL. The mapper builds a real
        // resources.tidal.com image URL from it (commit 1eaf92f) so Common's SimpleDownloadOrchestrator
        // artwork embedder can actually fetch the cover; this expectation was stale (still asserted the
        // raw id) after that intentional behavior change.
        Assert.Equal("https://resources.tidal.com/images/aa/bb/cc/1280x1280.jpg", sa.CoverArtUrls["original"]);
        Assert.Equal("https://tidal.com/browse/album/al1", sa.ExternalUrls["tidal"]);
        Assert.Equal("al1", sa.Metadata["tidal_id"]);
    }

    [Fact]
    public void ToStreamingAlbum_WithPrimaryArtistId_UsesArtistId_NotName()
    {
        // Arrange
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist A"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: new DateTime(2020, 1, 3),
            CoverArtId: "aa-bb-cc",
            IsAvailable: true,
            PrimaryArtistId: 789012L);

        // Act
        StreamingAlbum sa = this._mapper.ToStreamingAlbum(album);

        // Assert - Artist ID should be used, not name
        Assert.Equal("789012", sa.Artist.Id);
        Assert.Equal("Artist A", sa.Artist.Name);
    }

    [Fact]
    public void ToStreamingAlbum_WithoutPrimaryArtistId_FallsBackToArtistName()
    {
        // Arrange
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: ["Artist A"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: new DateTime(2020, 1, 3),
            CoverArtId: "aa-bb-cc",
            IsAvailable: true);

        // Act
        StreamingAlbum sa = this._mapper.ToStreamingAlbum(album);

        // Assert - Artist name should be used as ID when PrimaryArtistId is null
        Assert.Equal("Artist A", sa.Artist.Id);
        Assert.Equal("Artist A", sa.Artist.Name);
    }

    [Fact]
    public void ToStreamingSearchResults_CombinesAlbumsAndTracks()
    {
        TidalSearchResults results = new(
            Albums:
            [
                new("al1", "A", ["X"], [], [TidalQuality.Lossless], new DateTime(2020, 1, 1), "aa", true)
            ],
            Tracks:
            [
                new("t1","T", ["X"], "al1", "A", 1, 100, TidalQuality.High, true, new DateTime(2020,1,1))
            ],
            Artists: [],
            TotalCount: 2,
            HasMore: false);

        List<StreamingSearchResult> sr = this._mapper.ToStreamingSearchResults(results);
        Assert.Equal(2, sr.Count);
        Assert.Contains(sr, r => r.Type == StreamingSearchType.Album && r.Id == "al1");
        Assert.Contains(sr, r => r.Type == StreamingSearchType.Track && r.Id == "t1");
    }
}



