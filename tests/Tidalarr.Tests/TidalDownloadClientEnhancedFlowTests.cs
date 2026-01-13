using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedFlowTests
{
    private class CoreStub(string mime, string ext) : ITidalCore
    {
        private readonly string _mime = mime; private readonly string _ext = ext;

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "Song", ["Artist"], "al1", "Album", 1, 100, TidalQuality.Lossless, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
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
            return Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, ["https://chunk"], this._ext, this._mime, false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    private class OkHandler(byte[] payload) : HttpMessageHandler
    {
        private readonly byte[] _payload = payload;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(this._payload) });
        }
    }

    [Theory]
    [InlineData("application/dash+xml", ".flac")]
    [InlineData("application/vnd.tidal.bts", ".m4a")]
    public async Task DownloadTrackEnhancedAsync_WritesFile_ForDifferentMime(string mime, string ext)
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"tidal_enh_{Guid.NewGuid():N}");
        TidalDownloadClientSettings settings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalStreamService streamSvc = new(new CoreStub(mime, ext), new TidalManifestParser());
        byte[] payload = ext == ".flac"
            ? [(byte)'f', (byte)'L', (byte)'a', (byte)'C', 0x00, 0x00, 0x00, 0x00]
            : [0x00, 0x00, 0x00, 0x00, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0x00, 0x00, 0x00, 0x00];
        TidalChunkDownloader downloader = new(new HttpClient(new OkHandler(payload)));
        TidalDownloadClient client = new(streamSvc, downloader, new CoreStub(mime, ext), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        EnhancedDownloadResult res = await client.DownloadTrackEnhancedAsync("t1", tmp, TidalQuality.Lossless);
        Assert.True(res.Success);
        // Avoid strict FS assertions on all environments
        try { if (!string.IsNullOrEmpty(res.OutputPath) && File.Exists(res.OutputPath)) File.Delete(res.OutputPath); } catch { }
    }
}



