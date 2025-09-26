using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalStreamServiceQualityProbeTests
{
    private class ProbeCore : ITidalCore
    {
        private readonly HashSet<TidalQuality> _available;
        public ProbeCore(params TidalQuality[] available) { _available = new HashSet<TidalQuality>(available); }
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo("","",new(),"","",0,0,TidalQuality.High,true,DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            if (!_available.Contains(quality)) throw new InvalidOperationException("Unavailable");
            return Task.FromResult(new TidalStreamInfo(trackId, new[]{"u"}, ".flac", "audio/flac", false, null));
        }
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    [Fact]
    public async Task GetAvailableQualitiesForTrackAsync_ProbesInOrder()
    {
        var core = new ProbeCore(TidalQuality.Lossless, TidalQuality.High);
        var svc = new TidalStreamService(core, new TidalManifestParser());
        var list = await svc.GetAvailableQualitiesForTrackAsync("t1");
        Assert.Equal(new[]{ TidalQuality.Lossless, TidalQuality.High }, list);
    }
}



