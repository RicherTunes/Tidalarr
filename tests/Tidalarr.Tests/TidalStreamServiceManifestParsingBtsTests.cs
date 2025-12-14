using System.Text;
using Tidalarr.Domain.Streaming;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalStreamServiceManifestParsingBtsTests
{
    [Fact]
    public async Task GetStreamInfoWithManifestParsingAsync_BTS_ReturnsParsedInfo()
    {
        TidalManifestParser parser = new();
        TidalStreamService service = new(new TestsCommonCore(), parser);
        string btsJson = "{" +
                      "\"urls\":[\"https://u1\",\"https://u2\"]," +
                      "\"codecs\":\"flac\",\"mimeType\":\"audio/flac\",\"encryptionType\":\"NONE\"}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(btsJson));

        TidalStreamInfo info = await service.GetStreamInfoWithManifestParsingAsync("t1", TidalQuality.Lossless, encoded, "application/vnd.tidal.bts");
        Assert.Equal("t1", info.TrackId);
        Assert.Equal(".flac", info.FileExtension);
        Assert.True(info.ChunkUrls.Length >= 2);
    }
    [Fact]
    public async Task GetParsedManifestAsync_UsesManifestTokenWhenPlaybackSecurityTokenMissing()
    {
        string token = Convert.ToBase64String(new byte[] { 9, 8, 7, 6 });
        string manifestJson = "{" +
                           "\"urls\":[\"https://secure/u1\",\"https://secure/u2\"]," +
                           "\"codecs\":\"mp4a.40.2\"," +
                           "\"mimeType\":\"audio/mp4\"," +
                           "\"encryptionType\":\"AES_CTR\"," +
                           "\"encryptionKey\":\"" + token + "\"" +
                           "}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJson));
        TidalPlaybackInfoDto playback = new()
        {
            manifest = encoded,
            manifestMimeType = "application/vnd.tidal.bts",
            encryptionType = "AES_CTR",
            securityToken = null
        };

        TidalStreamService service = new(new ManifestTokenCoreStub(playback), new TidalManifestParser());
        TidalManifest manifest = await service.GetParsedManifestAsync("track-token", TidalQuality.Lossless);

        Assert.True(manifest.IsEncrypted);
        Assert.Equal(token, manifest.SecurityToken);
        Assert.Equal(".m4a", manifest.FileExtension);
    }
    [Fact]
    public async Task GetStreamInfoParsedAsync_UsesManifestTokenWhenPlaybackSecurityTokenMissing()
    {
        string token = Convert.ToBase64String(new byte[] { 4, 5, 6, 7 });
        string manifestJson = "{" +
                           "\"urls\":[\"https://secure/x1\",\"https://secure/x2\"]," +
                           "\"codecs\":\"mp4a.40.2\"," +
                           "\"mimeType\":\"audio/mp4\"," +
                           "\"encryptionType\":\"AES_CTR\"," +
                           "\"encryptionKey\":\"" + token + "\"" +
                           "}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJson));
        TidalPlaybackInfoDto playback = new()
        {
            manifest = encoded,
            manifestMimeType = "application/vnd.tidal.bts",
            encryptionType = "AES_CTR",
            securityToken = null
        };

        TidalStreamService service = new(new ManifestTokenCoreStub(playback), new TidalManifestParser());
        TidalStreamInfo info = await service.GetStreamInfoParsedAsync("track-info-token", TidalQuality.Lossless);

        Assert.True(info.IsEncrypted);
        Assert.Equal(token, info.SecurityToken);
        Assert.Equal(".m4a", info.FileExtension);
    }
    private class ManifestTokenCoreStub(TidalPlaybackInfoDto playback) : ITidalCore
    {
        private readonly TidalPlaybackInfoDto _playback = playback;
        private readonly TestsCommonCore _inner = new();

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return this._inner.GetTrackAsync(trackId, cancellationToken);
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return this._inner.GetAlbumAsync(albumId, cancellationToken);
        }

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return this._inner.GetAlbumTracksAsync(albumId, cancellationToken);
        }

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return this._inner.GetAlbumWithTracksAsync(albumId, cancellationToken);
        }

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            return this._inner.SearchAsync(query, limit, cancellationToken);
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return this._inner.GetStreamInfoAsync(trackId, quality, cancellationToken);
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return this._inner.IsAuthenticatedAsync();
        }

        public Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._playback);
        }
    }

    private class TestsCommonCore : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "", Array.Empty<string>(), "", "", 0, 0, TidalQuality.High, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", Array.Empty<string>(), Array.Empty<TidalTrackInfo>(), Array.Empty<TidalQuality>(), DateTime.UtcNow, "", true));
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
            return Task.FromResult(new TidalSearchResults(Array.Empty<TidalAlbumInfo>(), Array.Empty<TidalTrackInfo>(), Array.Empty<TidalArtistInfo>(), 0, false));
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






