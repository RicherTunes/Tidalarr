using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalStreamServiceQualityProbeTests
{
    private class ProbeCore(params TidalQuality[] available) : ITidalCore
    {
        private readonly HashSet<TidalQuality> _available = [.. available];

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
            return Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return !this._available.Contains(quality)
                ? throw new InvalidOperationException("Unavailable")
                : Task.FromResult(new TidalStreamInfo(trackId, ["u"], ".flac", "audio/flac", false, null));
        }
        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task GetAvailableQualitiesForTrackAsync_ProbesInOrder()
    {
        ProbeCore core = new ProbeCore(TidalQuality.Lossless, TidalQuality.High);
        TidalStreamService svc = new TidalStreamService(core, new TidalManifestParser());
        List<TidalQuality> list = await svc.GetAvailableQualitiesForTrackAsync("t1");
        Assert.Equal(new[] { TidalQuality.Lossless, TidalQuality.High }, list);
    }
}




