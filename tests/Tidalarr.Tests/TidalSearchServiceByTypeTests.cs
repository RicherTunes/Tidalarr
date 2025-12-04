using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests;

public class TidalSearchServiceByTypeTests
{
    private class CoreFake : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo("", "", new(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", new(), new(), new(), DateTime.MinValue, "", true));
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
            TidalAlbumInfo album = new TidalAlbumInfo("al1", "Album", new() { "Artist" }, new(), new() { TidalQuality.Lossless }, DateTime.UtcNow, "c", true);
            TidalTrackInfo track = new TidalTrackInfo("t1", "Song", new() { "Artist" }, "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow);
            return Task.FromResult(new TidalSearchResults(new() { album }, new() { track }, 2, false));
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
    public async Task SearchByType_Album_OnlyAlbumsReturned()
    {
        TidalSearchService svc = new TidalSearchService(new CoreFake(), new TidalQualityDetector());
        TidalSearchResults res = await svc.SearchByTypeAsync("query", TidalSearchType.Album);
        _ = Assert.Single(res.Albums);
        Assert.Empty(res.Tracks);
    }

    [Fact]
    public async Task SearchByType_Track_OnlyTracksReturned()
    {
        TidalSearchService svc = new TidalSearchService(new CoreFake(), new TidalQualityDetector());
        TidalSearchResults res = await svc.SearchByTypeAsync("query", TidalSearchType.Track);
        _ = Assert.Single(res.Tracks);
        Assert.Empty(res.Albums);
    }

    [Fact]
    public async Task SearchByType_All_ReturnsCombined()
    {
        TidalSearchService svc = new TidalSearchService(new CoreFake(), new TidalQualityDetector());
        TidalSearchResults res = await svc.SearchByTypeAsync("query", TidalSearchType.All);
        _ = Assert.Single(res.Tracks);
        _ = Assert.Single(res.Albums);
    }
}




