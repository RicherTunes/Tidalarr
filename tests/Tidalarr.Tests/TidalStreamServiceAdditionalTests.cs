using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalStreamServiceAdditionalTests
{
    private class CoreStub : ITidalCore
    {
        private readonly TidalStreamInfo _info;
        public CoreStub(TidalStreamInfo info) { _info = info; }
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo("","",new(),"","",0,0,TidalQuality.High,true,DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(_info);
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    [Fact]
    public async Task ValidateStreamAvailability_EmptyChunks_ReturnsFalse()
    {
        var info = new TidalStreamInfo("t1", Array.Empty<string>(), ".flac", "audio/flac", false, null);
        var svc = new TidalStreamService(new CoreStub(info), new TidalManifestParser());
        var ok = await svc.ValidateStreamAvailabilityAsync("t1", TidalQuality.High);
        Assert.False(ok);
    }

    [Fact]
    public async Task ValidateStreamAvailability_EmptyExtension_ReturnsFalse()
    {
        var info = new TidalStreamInfo("t1", new[] {"https://u"}, string.Empty, "audio/flac", false, null);
        var svc = new TidalStreamService(new CoreStub(info), new TidalManifestParser());
        var ok = await svc.ValidateStreamAvailabilityAsync("t1", TidalQuality.High);
        Assert.False(ok);
    }
}



