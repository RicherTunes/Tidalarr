using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalDownloadClientValidationTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
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
            return Task.FromResult(new TidalStreamInfo(trackId, ["https://chunk"], ".flac", "audio/flac", false, null));
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
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("ok")) });
        }
    }
    private class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        }
    }

    [Fact]
    public async Task ValidateDownloadAsync_Success_WhenChunkAccessible()
    {
        TidalStreamService streamSvc = new(new CoreStub(), new TidalManifestParser());
        TidalChunkDownloader downloader = new(new HttpClient(new OkHandler()));
        TidalDownloadClientSettings settings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalDownloadClient client = new(streamSvc, downloader, new CoreStub(), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);
        bool ok = await client.ValidateDownloadAsync("t1", TidalQuality.Lossless);
        Assert.True(ok);
    }

    [Fact]
    public async Task ValidateDownloadAsync_False_WhenChunkInaccessible()
    {
        TidalStreamService streamSvc = new(new CoreStub(), new TidalManifestParser());
        TidalChunkDownloader downloader = new(new HttpClient(new FailHandler()));
        TidalDownloadClientSettings settings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalDownloadClient client = new(streamSvc, downloader, new CoreStub(), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);
        bool ok = await client.ValidateDownloadAsync("t1", TidalQuality.Lossless);
        Assert.False(ok);
    }
}





