using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedTests
{
    private class CoreStub(params string[] chunks) : ITidalCore
    {
        private readonly string[] _chunks = chunks.Length > 0 ? chunks : ["https://chunk1"];

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
            return Task.FromResult(new TidalStreamInfo(trackId, this._chunks, ".flac", "audio/flac", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    private class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // return some bytes for any chunk URL
            byte[] flacHeader = [(byte)'f', (byte)'L', (byte)'a', (byte)'C', 0x00, 0x00, 0x00, 0x00];
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(flacHeader)
            });
        }
    }

    private class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway));
        }
    }

    [Fact]
    public async Task DownloadTrackWithMetadataAsync_Succeeds_AndWritesFile()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"tidal_test_{Guid.NewGuid():N}.flac");
        TidalDownloadClientSettings settings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalStreamService streamSvc = new(new CoreStub("https://chunk1"), new TidalManifestParser());
        TidalChunkDownloader downloader = new(new HttpClient(new OkHandler()), segmentPolicy: TidalTestPolicies.Resolving);
        TidalDownloadClient client = new(streamSvc, downloader, new CoreStub(), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        StreamingDownloadResult result = await client.DownloadTrackWithMetadataAsync("t1", tmp, TidalQuality.Lossless);
        Assert.True(result.Success);
        Assert.True(File.Exists(tmp));

        try { File.Delete(tmp); } catch { }
    }

    [Fact]
    public async Task DownloadTrackWithMetadataAsync_Failure_ReturnsError()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"tidal_test_{Guid.NewGuid():N}.flac");
        TidalDownloadClientSettings settings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalStreamService streamSvc = new(new CoreStub("https://chunk1"), new TidalManifestParser());
        TidalChunkDownloader downloader = new(new HttpClient(new FailingHandler()), segmentPolicy: TidalTestPolicies.Resolving);
        TidalDownloadClient client = new(streamSvc, downloader, new CoreStub(), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        StreamingDownloadResult result = await client.DownloadTrackWithMetadataAsync("t1", tmp, TidalQuality.Lossless);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        if (File.Exists(tmp)) { try { File.Delete(tmp); } catch { } }
    }
}




