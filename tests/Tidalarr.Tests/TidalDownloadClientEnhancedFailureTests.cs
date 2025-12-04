using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedFailureTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "Song", new() { "Artist" }, "al1", "Album", 1, 100, TidalQuality.Lossless, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", new(), new(), new(), DateTime.MinValue, "", true));
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
            return Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
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

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("network error");
        }
    }

    [Fact]
    public async Task DownloadTrackEnhancedAsync_WhenDownloaderThrows_ReturnsError()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"tidal_enh_fail_{Guid.NewGuid():N}");
        TidalDownloadClientSettings settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalStreamService streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        TidalChunkDownloader downloader = new TidalChunkDownloader(new HttpClient(new ThrowingHandler()));
        TidalDownloadClient client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        EnhancedDownloadResult res = await client.DownloadTrackEnhancedAsync("t1", tmp, TidalQuality.Lossless);
        Assert.False(res.Success);
        Assert.NotNull(res.ErrorMessage);
    }
}





