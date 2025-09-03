using Tidalarr.Domain.Streaming;
using Tidalarr.Core.Models;
using Xunit;

namespace Tidalarr.Tests;

public class TidalStreamServiceInvalidBtsTests
{
    [Fact]
    public async Task GetStreamInfoWithManifestParsingAsync_InvalidBTS_ThrowsFormatException()
    {
        var svc = new Tidalarr.Domain.Streaming.TidalStreamService(new DummyCore(), new TidalManifestParser());
        await Assert.ThrowsAsync<FormatException>(() => svc.GetStreamInfoWithManifestParsingAsync("t1", TidalQuality.Lossless, "not-base64", "application/vnd.tidal.bts"));
    }

    private class DummyCore : Tidalarr.Core.Interfaces.ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId,"",new(),"","",0,0,TidalQuality.High,true,DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.UtcNow,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }
}

