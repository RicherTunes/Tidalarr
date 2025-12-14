using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalStreamServiceAdditionalTests
{
    private class CoreStub(TidalStreamInfo info) : ITidalCore
    {
        private readonly TidalStreamInfo _info = info;

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
            return Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo>(), new List<TidalTrackInfo>(), 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._info);
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task ValidateStreamAvailability_EmptyChunks_ReturnsFalse()
    {
        TidalStreamInfo info = new("t1", [], ".flac", "audio/flac", false, null);
        TidalStreamService svc = new(new CoreStub(info), new TidalManifestParser());
        bool ok = await svc.ValidateStreamAvailabilityAsync("t1", TidalQuality.High);
        Assert.False(ok);
    }

    [Fact]
    public async Task ValidateStreamAvailability_EmptyExtension_ReturnsFalse()
    {
        TidalStreamInfo info = new("t1", ["https://u"], string.Empty, "audio/flac", false, null);
        TidalStreamService svc = new(new CoreStub(info), new TidalManifestParser());
        bool ok = await svc.ValidateStreamAvailabilityAsync("t1", TidalQuality.High);
        Assert.False(ok);
    }
}




