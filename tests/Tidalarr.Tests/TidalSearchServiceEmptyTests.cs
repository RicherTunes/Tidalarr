using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;
using Xunit;

namespace Tidalarr.Tests;

public class TidalSearchServiceEmptyTests
{
    private class EmptyCore : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo("","",new(),"","",0,0,TidalQuality.High,true,DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    [Fact]
    public async Task SearchWithQualityDetection_NoResults_ReturnsEmpty()
    {
        var svc = new TidalSearchService(new EmptyCore(), new TidalQualityDetector());
        var res = await svc.SearchWithQualityDetectionAsync("query");
        Assert.Empty(res.Albums);
        Assert.Empty(res.Tracks);
        Assert.Equal(0, res.TotalCount);
    }

    [Fact]
    public async Task SearchWithQualityDetection_InvalidQuery_Throws()
    {
        var svc = new TidalSearchService(new EmptyCore(), new TidalQualityDetector());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SearchWithQualityDetectionAsync(" "));
    }
}




