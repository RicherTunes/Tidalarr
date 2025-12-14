using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

public class IndexerDiagnosticsTests
{
    private class CoreAuthFalse : ITidalCore
    {
        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(false);
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "", Array.Empty<string>(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", Array.Empty<string>(), Array.Empty<TidalTrackInfo>(), Array.Empty<TidalQuality>(), DateTime.MinValue, "", true));
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
            return Task.FromResult(new TidalSearchResults(Array.Empty<TidalAlbumInfo>(), Array.Empty<TidalTrackInfo>(), Array.Empty<TidalArtistInfo>(), 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        }
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_InvalidSettings_ReturnsIX100AndCodes()
    {
        TidalIndexerSettings settings = new()
        {
            ConfigPath = "", // invalid
            RedirectUrl = ""  // invalid
        };
        TidalIndexer indexer = new(new Application.Services.TidalSearchService(new CoreAuthFalse(), new Domain.Quality.TidalQualityDetector(), null), new CoreAuthFalse(), settings, NullLogger.Instance);

        Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>> result = indexer.ValidateSettingsWithDiagnostics();
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("IX100", result.Error!.Metadata["id"]);
        Assert.True(result.Error!.Metadata.ContainsKey("errors"));
    }

    [Fact]
    public async Task InitializeWithDiagnostics_AuthFailure_ReturnsIX200()
    {
        TidalIndexerSettings settings = new()
        {
            ConfigPath = Path.GetTempPath(),
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=state",
            TidalMarket = "US"
        };
        TidalIndexer indexer = new(new Application.Services.TidalSearchService(new CoreAuthFalse(), new Domain.Quality.TidalQualityDetector(), null), new CoreAuthFalse(), settings, NullLogger.Instance);

        Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>> result = await indexer.InitializeWithDiagnosticsAsync();
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("IX200", result.Error!.Metadata["id"]);
    }
}
