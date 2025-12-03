using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class DownloadValidationDiagnosticsTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId, "", new List<string>(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("", "", new List<string>(), new List<TidalTrackInfo>(), new List<TidalQuality>(), DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo>(), new List<TidalTrackInfo>(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, new[] { "https://chunk" }, ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("ok")) });
    }
    private class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
    }

    [Fact]
    public async Task ValidateDownloadWithDiagnostics_Succeeds_WithCode()
    {
        var streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        var result = await client.ValidateDownloadWithDiagnosticsAsync("t1", TidalQuality.Lossless);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("DL000", result.Value!["id"]);
        Assert.Equal("t1", result.Value!["trackId"]);
    }

    [Fact]
    public async Task ValidateDownloadWithDiagnostics_Fails_WithStableCode()
    {
        var streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new FailHandler()));
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        var result = await client.ValidateDownloadWithDiagnosticsAsync("t1", TidalQuality.Lossless);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DL001", result.Error!.Metadata["id"]);
        Assert.Equal("t1", result.Error!.Metadata["trackId"]);
    }
}
