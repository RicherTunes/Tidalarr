using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalIndexerProtectedTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "Song", new List<string> { "Artist" }, "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo(albumId, "Album", new List<string> { "Artist" }, new List<TidalTrackInfo> { new("t1", "Song", new List<string> { "Artist" }, albumId, "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow) }, new List<TidalQuality> { TidalQuality.High }, DateTime.UtcNow, "cover", true));
        }

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TidalTrackInfo>());
        }

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return GetAlbumAsync(albumId, cancellationToken);
        }

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo>(), new List<TidalTrackInfo> { new("t1", "Song", new List<string> { "Artist" }, "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow) }, new List<TidalArtistInfo>(), 1, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, ["u"], ".flac", "audio/flac", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    private class IndexerExposed(TidalSearchService s, ITidalCore c, TidalIndexerSettings st) : TidalIndexer(s, c, st, NullLogger.Instance)
    {
        public Task<List<Lidarr.Plugin.Abstractions.Models.StreamingTrack>> ExposeSearchTracksAsync(string q)
        {
            return base.SearchTracksAsync(q);
        }

        public Task<Lidarr.Plugin.Abstractions.Models.StreamingAlbum> ExposeGetAlbumDetailsAsync(string id)
        {
            return base.GetAlbumDetailsAsync(id);
        }
    }

    [Fact]
    public async Task SearchTracksAsync_MapsTracks()
    {
        TidalIndexerSettings settings = new() { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        TidalSearchService searchSvc = new(new CoreStub(), new Domain.Quality.TidalQualityDetector(), null);
        IndexerExposed indexer = new(searchSvc, new CoreStub(), settings);
        List<Lidarr.Plugin.Abstractions.Models.StreamingTrack> tracks = await indexer.ExposeSearchTracksAsync("query");
        Assert.NotEmpty(tracks);
        Assert.Equal("Song", tracks[0].Title);
    }

    [Fact]
    public async Task GetAlbumDetailsAsync_ReturnsMappedAlbum()
    {
        TidalIndexerSettings settings = new() { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        TidalSearchService searchSvc = new(new CoreStub(), new Domain.Quality.TidalQualityDetector(), null);
        IndexerExposed indexer = new(searchSvc, new CoreStub(), settings);
        Lidarr.Plugin.Abstractions.Models.StreamingAlbum album = await indexer.ExposeGetAlbumDetailsAsync("al1");
        Assert.Equal("Album", album.Title);
    }
}




