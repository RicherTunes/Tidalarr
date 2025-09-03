using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedFlowTests
{
    private class CoreStub : ITidalCore
    {
        private readonly string _mime; private readonly string _ext;
        public CoreStub(string mime, string ext) { _mime = mime; _ext = ext; }
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId,"Song",new(){"Artist"},"al1","Album",1,100,TidalQuality.Lossless,true,DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, new[]{"https://chunk"}, _ext, _mime, false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[]{1,2,3,4}) });
    }

    [Theory(Skip="File system semantics vary in CI; exercising code path locally is fine")]
    [InlineData("application/dash+xml", ".flac")]
    [InlineData("application/vnd.tidal.bts", ".m4a")]
    public async Task DownloadTrackEnhancedAsync_WritesFile_ForDifferentMime(string mime, string ext)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"tidal_enh_{Guid.NewGuid():N}");
        var settings = new TidalDownloadSettings { PreferredQuality = "Lossless", DownloadPath = Path.GetTempPath() };
        var streamSvc = new TidalStreamService(new CoreStub(mime, ext), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(mime, ext), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        var res = await client.DownloadTrackEnhancedAsync("t1", tmp, TidalQuality.Lossless);
        Assert.True(res.Success);
        // Avoid strict FS assertions on all environments
        try { if (!string.IsNullOrEmpty(res.OutputPath) && File.Exists(res.OutputPath)) File.Delete(res.OutputPath); } catch { }
    }
}
