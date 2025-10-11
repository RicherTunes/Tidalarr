using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalIndexerProtectedTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId, "Song", new() { "Artist" }, "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo(albumId, "Album", new() { "Artist" }, new() { new TidalTrackInfo("t1", "Song", new() { "Artist" }, albumId, "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow) }, new() { TidalQuality.High }, DateTime.UtcNow, "cover", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalSearchResults(new(), new() { new TidalTrackInfo("t1", "Song", new() { "Artist" }, "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow) }, 1, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, new[] { "u" }, ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class IndexerExposed : TidalIndexer
    {
        public IndexerExposed(TidalSearchService s, ITidalCore c, TidalIndexerSettings st)
            : base(s, c, st, NullLogger.Instance) { }
        public Task<System.Collections.Generic.List<Lidarr.Plugin.Abstractions.Models.StreamingTrack>> ExposeSearchTracksAsync(string q) => base.SearchTracksAsync(q);
        public Task<Lidarr.Plugin.Abstractions.Models.StreamingAlbum> ExposeGetAlbumDetailsAsync(string id) => base.GetAlbumDetailsAsync(id);
    }

    [Fact]
    public async Task SearchTracksAsync_MapsTracks()
    {
        var settings = new TidalIndexerSettings { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = System.IO.Path.GetTempPath() };
        var searchSvc = new Tidalarr.Application.Services.TidalSearchService(new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector());
        var indexer = new IndexerExposed(searchSvc, new CoreStub(), settings);
        var tracks = await indexer.ExposeSearchTracksAsync("query");
        Assert.NotEmpty(tracks);
        Assert.Equal("Song", tracks[0].Title);
    }

    [Fact]
    public async Task GetAlbumDetailsAsync_ReturnsMappedAlbum()
    {
        var settings = new TidalIndexerSettings { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = System.IO.Path.GetTempPath() };
        var searchSvc = new Tidalarr.Application.Services.TidalSearchService(new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector());
        var indexer = new IndexerExposed(searchSvc, new CoreStub(), settings);
        var album = await indexer.ExposeGetAlbumDetailsAsync("al1");
        Assert.Equal("Album", album.Title);
    }
}




