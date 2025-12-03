using System.Text;
using Tidalarr.Domain.Streaming;
using Tidalarr.Core.Interfaces;
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
    [Fact]
    public async Task GetParsedManifestAsync_UsesManifestTokenWhenPlaybackSecurityTokenMissing()
    {
        var token = Convert.ToBase64String(new byte[] { 9, 8, 7, 6 });
        var manifestJson = "{" +
                           "\"urls\":[\"https://secure/u1\",\"https://secure/u2\"]," +
                           "\"codecs\":\"mp4a.40.2\"," +
                           "\"mimeType\":\"audio/mp4\"," +
                           "\"encryptionType\":\"AES_CTR\"," +
                           "\"encryptionKey\":\"" + token + "\"" +
                           "}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJson));
        var playback = new TidalPlaybackInfoDto
        {
            manifest = encoded,
            manifestMimeType = "application/vnd.tidal.bts",
            encryptionType = "AES_CTR",
            securityToken = null
        };

        var service = new Tidalarr.Domain.Streaming.TidalStreamService(new ManifestTokenCoreStub(playback), new TidalManifestParser());
        var manifest = await service.GetParsedManifestAsync("track-token", TidalQuality.Lossless);

        Assert.True(manifest.IsEncrypted);
        Assert.Equal(token, manifest.SecurityToken);
        Assert.Equal(".m4a", manifest.FileExtension);
    }
    [Fact]
    public async Task GetStreamInfoParsedAsync_UsesManifestTokenWhenPlaybackSecurityTokenMissing()
    {
        var token = Convert.ToBase64String(new byte[] { 4, 5, 6, 7 });
        var manifestJson = "{" +
                           "\"urls\":[\"https://secure/x1\",\"https://secure/x2\"]," +
                           "\"codecs\":\"mp4a.40.2\"," +
                           "\"mimeType\":\"audio/mp4\"," +
                           "\"encryptionType\":\"AES_CTR\"," +
                           "\"encryptionKey\":\"" + token + "\"" +
                           "}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJson));
        var playback = new TidalPlaybackInfoDto
        {
            manifest = encoded,
            manifestMimeType = "application/vnd.tidal.bts",
            encryptionType = "AES_CTR",
            securityToken = null
        };

        var service = new Tidalarr.Domain.Streaming.TidalStreamService(new ManifestTokenCoreStub(playback), new TidalManifestParser());
        var info = await service.GetStreamInfoParsedAsync("track-info-token", TidalQuality.Lossless);

        Assert.True(info.IsEncrypted);
        Assert.Equal(token, info.SecurityToken);
        Assert.Equal(".m4a", info.FileExtension);
    }
    private class ManifestTokenCoreStub : ITidalCore
    {
        private readonly TidalPlaybackInfoDto _playback;
        private readonly TestsCommonCore _inner = new();

        public ManifestTokenCoreStub(TidalPlaybackInfoDto playback)
        {
            _playback = playback;
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => _inner.GetTrackAsync(trackId, cancellationToken);

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => _inner.GetAlbumAsync(albumId, cancellationToken);

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => _inner.GetAlbumTracksAsync(albumId, cancellationToken);

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => _inner.GetAlbumWithTracksAsync(albumId, cancellationToken);

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => _inner.SearchAsync(query, limit, cancellationToken);

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => _inner.GetStreamInfoAsync(trackId, quality, cancellationToken);

        public Task<bool> IsAuthenticatedAsync()
            => _inner.IsAuthenticatedAsync();

        public Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(_playback);
    }

    private class TestsCommonCore : Tidalarr.Core.Interfaces.ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId, "", new List<string>(), "", "", 0, 0, TidalQuality.High, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("", "", new List<string>(), new List<TidalTrackInfo>(), new List<TidalQuality>(), DateTime.UtcNow, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo>(), new List<TidalTrackInfo>(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }
}
