using Tidalarr.Domain.Streaming;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalStreamServiceInvalidBtsTests
{
    [Fact]
    public async Task GetStreamInfoWithManifestParsingAsync_InvalidBTS_ThrowsFormatException()
    {
        TidalStreamService svc = new(new DummyCore(), new TidalManifestParser());
        _ = await Assert.ThrowsAsync<FormatException>(() => svc.GetStreamInfoWithManifestParsingAsync("t1", TidalQuality.Lossless, "not-base64", "application/vnd.tidal.bts"));
    }

    private class DummyCore : Core.Interfaces.ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "", new List<string>(), "", "", 0, 0, TidalQuality.High, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", new List<string>(), new List<TidalTrackInfo>(), new List<TidalQuality>(), DateTime.UtcNow, "", true));
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
}




