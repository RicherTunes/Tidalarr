using System;
using System.Collections.Generic;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;
using Xunit;

namespace Tidalarr.Tests;

public class TidalModelMapperTests
{
    private readonly TidalModelMapper _mapper = new();

    [Fact]
    public void ToStreamingTrack_MapsCoreFields_AndMetadata()
    {
        var track = new TidalTrackInfo(
            Id: "t1",
            Title: "Song",
            Artists: new List<string> { "Artist A", "Artist B" },
            AlbumId: "al1",
            AlbumTitle: "Album",
            TrackNumber: 3,
            Duration: 200,
            Quality: TidalQuality.Lossless,
            IsAvailable: true,
            ReleaseDate: new DateTime(2020, 1, 2));

        var st = _mapper.ToStreamingTrack(track);
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
    public void ToStreamingAlbum_MapsQualities_CoverUrls_AndMetadata()
    {
        var album = new TidalAlbumInfo(
            Id: "al1",
            Title: "Album",
            Artists: new List<string> { "Artist A" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.High, TidalQuality.HiRes },
            ReleaseDate: new DateTime(2020, 1, 3),
            CoverArtId: "aa-bb-cc",
            IsAvailable: true);

        var sa = _mapper.ToStreamingAlbum(album);
        Assert.Equal("al1", sa.Id);
        Assert.Contains(sa.AvailableQualities, q => q.Id == "HIGH");
        Assert.Contains(sa.AvailableQualities, q => q.Id == "HI_RES");
        Assert.Equal("aa-bb-cc", sa.CoverArtUrls["original"]);
        Assert.Equal("https://tidal.com/browse/album/al1", sa.ExternalUrls["tidal"]);
        Assert.Equal("al1", sa.Metadata["tidal_id"]);
    }

    [Fact]
    public void ToStreamingSearchResults_CombinesAlbumsAndTracks()
    {
        var results = new TidalSearchResults(
            Albums: new List<TidalAlbumInfo>
            {
                new("al1","A", new List<string>{"X"}, new List<TidalTrackInfo>(), new List<TidalQuality>{TidalQuality.Lossless}, new DateTime(2020,1,1), "aa", true)
            },
            Tracks: new List<TidalTrackInfo>
            {
                new("t1","T", new List<string>{"X"}, "al1", "A", 1, 100, TidalQuality.High, true, new DateTime(2020,1,1))
            },
            TotalCount: 2,
            HasMore: false);

        var sr = _mapper.ToStreamingSearchResults(results);
        Assert.Equal(2, sr.Count);
        Assert.Contains(sr, r => r.Type == StreamingSearchType.Album && r.Id == "al1");
        Assert.Contains(sr, r => r.Type == StreamingSearchType.Track && r.Id == "t1");
    }
}




