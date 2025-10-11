using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class IndexerDiagnosticsTests
{
    private class CoreAuthFalse : ITidalCore
    {
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(false);
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId, "", new(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".flac", "audio/flac", false, null));
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_InvalidSettings_ReturnsIX100AndCodes()
    {
        var settings = new TidalIndexerSettings
        {
            ConfigPath = "", // invalid
            RedirectUrl = ""  // invalid
        };
        var indexer = new TidalIndexer(new Application.Services.TidalSearchService(new CoreAuthFalse(), new Domain.Quality.TidalQualityDetector(), null), new CoreAuthFalse(), settings, NullLogger.Instance);

        var result = indexer.ValidateSettingsWithDiagnostics();
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("IX100", result.Error!.Metadata["id"]);
        Assert.True(result.Error!.Metadata.ContainsKey("errors"));
    }

    [Fact]
    public async Task InitializeWithDiagnostics_AuthFailure_ReturnsIX200()
    {
        var settings = new TidalIndexerSettings
        {
            ConfigPath = System.IO.Path.GetTempPath(),
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=state",
            TidalMarket = "US"
        };
        var indexer = new TidalIndexer(new Application.Services.TidalSearchService(new CoreAuthFalse(), new Domain.Quality.TidalQualityDetector(), null), new CoreAuthFalse(), settings, NullLogger.Instance);

        var result = await indexer.InitializeWithDiagnosticsAsync();
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("IX200", result.Error!.Metadata["id"]);
    }
}
