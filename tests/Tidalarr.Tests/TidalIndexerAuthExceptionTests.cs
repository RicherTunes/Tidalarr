using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalIndexerAuthExceptionTests
{
    private class ThrowAuthCore : ITidalCore
    {
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
            return Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            throw new InvalidOperationException("auth error");
        }
    }

    [Fact]
    public async Task InitializeAsync_ReturnsInvalid_WhenAuthThrows()
    {
        TidalIndexerSettings settings = new() { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        ThrowAuthCore core = new();
        TidalIndexer indexer = new(new TidalSearchService(core, new Domain.Quality.TidalQualityDetector()), core, settings, NullLogger.Instance);
        FluentValidation.Results.ValidationResult res = await indexer.InitializeAsync();
        Assert.False(res.IsValid);
    }
}




