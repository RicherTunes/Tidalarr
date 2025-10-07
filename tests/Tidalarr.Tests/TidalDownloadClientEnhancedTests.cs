using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedTests
{
    private class CoreStub : ITidalCore
    {
        private readonly string[] _chunks;
        public CoreStub(params string[] chunks) { _chunks = chunks.Length > 0 ? chunks : new[]{"https://chunk1"}; }
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId,"Song",new(){"Artist"},"al1","Album",1,100,TidalQuality.Lossless,true,DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, _chunks, ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // return some bytes for any chunk URL
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("audio"))
            });
        }
    }

    private class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway));
    }

    [Fact]
    public async Task DownloadTrackWithMetadataAsync_Succeeds_AndWritesFile()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"tidal_test_{Guid.NewGuid():N}.flac");
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        var streamSvc = new TidalStreamService(new CoreStub("https://chunk1"), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        var result = await client.DownloadTrackWithMetadataAsync("t1", tmp, TidalQuality.Lossless);
        Assert.True(result.Success);
        Assert.True(File.Exists(tmp));

        try { File.Delete(tmp); } catch { }
    }

    [Fact]
    public async Task DownloadTrackWithMetadataAsync_Failure_ReturnsError()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"tidal_test_{Guid.NewGuid():N}.flac");
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        var streamSvc = new TidalStreamService(new CoreStub("https://chunk1"), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new FailingHandler()));
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        var result = await client.DownloadTrackWithMetadataAsync("t1", tmp, TidalQuality.Lossless);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        if (File.Exists(tmp)) { try { File.Delete(tmp); } catch { } }
    }
}





