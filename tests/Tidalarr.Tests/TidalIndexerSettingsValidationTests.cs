using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalIndexerSettingsValidationTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo("","",new(),"","",0,0,TidalQuality.High,true,DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    [Fact]
    public async Task ValidateSettings_MissingFields_ReturnsErrors()
    {
        var settings = new TidalIndexerSettings { RedirectUrl = "", ConfigPath = "" };
        var core = new CoreStub();
        var indexer = new TidalIndexer(new Tidalarr.Application.Services.TidalSearchService(core, new Tidalarr.Domain.Quality.TidalQualityDetector()), core, settings);
        var result = await indexer.InitializeAsync();
        Assert.False(result.IsValid);
    }
}
