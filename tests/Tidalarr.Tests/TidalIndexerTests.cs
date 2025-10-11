using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalIndexerTests
{
    private class CoreFake : ITidalCore
    {
        public bool Authenticated = true;
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo("", "", new(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("", "", new(), new(), new(), DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            var album = new TidalAlbumInfo("al1", "A", new() { "X" }, new(), new() { TidalQuality.Lossless }, DateTime.UtcNow, "c", true);
            return Task.FromResult(new TidalSearchResults(new() { album }, new(), 1, false));
        }
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(Authenticated);
    }

    [Fact]
    public async Task InitializeAndSearch_ReturnsResults()
    {
        var core = new CoreFake();
        var search = new TidalSearchService(core, new Tidalarr.Domain.Quality.TidalQualityDetector());
        var settings = new TidalIndexerSettings { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = System.IO.Path.GetTempPath() };

        var indexer = new TidalIndexer(search, core, settings, NullLogger.Instance);
        var init = await indexer.InitializeAsync();
        Assert.True(init.IsValid);

        var results = await indexer.SearchAsync("Daft Punk");
        Assert.NotEmpty(results);
        Assert.Equal("A", results[0].Title);
    }

    [Fact]
    public async Task Initialize_FailsWhenNotAuthenticated()
    {
        var core = new CoreFake { Authenticated = false };
        var search = new TidalSearchService(core, new Tidalarr.Domain.Quality.TidalQualityDetector());
        var settings = new TidalIndexerSettings { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = System.IO.Path.GetTempPath() };
        var indexer = new TidalIndexer(search, core, settings, NullLogger.Instance);

        var result = await indexer.InitializeAsync();
        Assert.False(result.IsValid);
    }
}




