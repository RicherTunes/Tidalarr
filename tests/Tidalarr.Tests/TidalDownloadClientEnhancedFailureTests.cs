using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedFailureTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId, "Song", new() { "Artist" }, "al1", "Album", 1, 100, TidalQuality.Lossless, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("", "", new(), new(), new(), DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, new[] { "https://chunk" }, ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("network error");
    }

    [Fact]
    public async Task DownloadTrackEnhancedAsync_WhenDownloaderThrows_ReturnsError()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"tidal_enh_fail_{Guid.NewGuid():N}");
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        var streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new ThrowingHandler()));
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        var res = await client.DownloadTrackEnhancedAsync("t1", tmp, TidalQuality.Lossless);
        Assert.False(res.Success);
        Assert.NotNull(res.ErrorMessage);
    }
}





