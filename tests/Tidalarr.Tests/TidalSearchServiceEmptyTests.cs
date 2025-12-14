using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests;

public class TidalSearchServiceEmptyTests
{
    private class EmptyCore : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo("", "", new List<string>(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", new List<string>(), new List<TidalTrackInfo>(), new List<TidalQuality>(), DateTime.MinValue, "", true));
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
            return Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo>(), new List<TidalTrackInfo>(), new List<TidalArtistInfo>(), 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task SearchWithQualityDetection_NoResults_ReturnsEmpty()
    {
        TidalSearchService svc = new(new EmptyCore(), new TidalQualityDetector());
        TidalSearchResults res = await svc.SearchWithQualityDetectionAsync("query");
        Assert.Empty(res.Albums);
        Assert.Empty(res.Tracks);
        Assert.Equal(0, res.TotalCount);
    }

    [Fact]
    public async Task SearchWithQualityDetection_InvalidQuery_Throws()
    {
        TidalSearchService svc = new(new EmptyCore(), new TidalQualityDetector());
        _ = await Assert.ThrowsAsync<ArgumentException>(() => svc.SearchWithQualityDetectionAsync(" "));
    }
}




