using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalIndexerTests
{
    private class CoreFake : ITidalCore
    {
        public bool Authenticated = true;
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
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
            TidalAlbumInfo album = new("al1", "A", ["X"], [], [TidalQuality.Lossless], DateTime.UtcNow, "c", true);
            return Task.FromResult(new TidalSearchResults([album], [], [], 1, false));
        }
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(this.Authenticated);
        }
    }

    [Fact]
    public async Task InitializeAndSearch_ReturnsResults()
    {
        CoreFake core = new();
        TidalSearchService search = new(core, new Domain.Quality.TidalQualityDetector());
        TidalIndexerSettings settings = new() { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };

        TidalIndexer indexer = new(search, core, settings, NullLogger.Instance);
        FluentValidation.Results.ValidationResult init = await indexer.InitializeAsync();
        Assert.True(init.IsValid);

        List<Lidarr.Plugin.Abstractions.Models.StreamingAlbum> results = await indexer.SearchAsync("Daft Punk");
        Assert.NotEmpty(results);
        Assert.Equal("A", results[0].Title);
    }

    [Fact]
    public async Task Initialize_FailsWhenNotAuthenticated()
    {
        CoreFake core = new() { Authenticated = false };
        TidalSearchService search = new(core, new Domain.Quality.TidalQualityDetector());
        TidalIndexerSettings settings = new() { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        TidalIndexer indexer = new(search, core, settings, NullLogger.Instance);

        FluentValidation.Results.ValidationResult result = await indexer.InitializeAsync();
        Assert.False(result.IsValid);
    }
}




