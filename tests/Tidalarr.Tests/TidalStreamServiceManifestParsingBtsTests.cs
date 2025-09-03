using System.Text;
using Tidalarr.Domain.Streaming;
using Tidalarr.Core.Models;
using Xunit;

namespace Tidalarr.Tests;

public class TidalStreamServiceManifestParsingBtsTests
{
    [Fact]
    public async Task GetStreamInfoWithManifestParsingAsync_BTS_ReturnsParsedInfo()
    {
        var parser = new TidalManifestParser();
        var service = new Tidalarr.Domain.Streaming.TidalStreamService(new TestsCommonCore(), parser);
        var btsJson = "{" +
                      "\"urls\":[\"https://u1\",\"https://u2\"]," +
                      "\"codecs\":\"flac\",\"mimeType\":\"audio/flac\",\"encryptionType\":\"NONE\"}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(btsJson));

        var info = await service.GetStreamInfoWithManifestParsingAsync("t1", TidalQuality.Lossless, encoded, "application/vnd.tidal.bts");
        Assert.Equal("t1", info.TrackId);
        Assert.Equal(".flac", info.FileExtension);
        Assert.True(info.ChunkUrls.Length >= 2);
    }

    private class TestsCommonCore : Tidalarr.Core.Interfaces.ITidalCore
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

