using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

public class DownloadValidationDiagnosticsTests
{
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "", new(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
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
    public async Task ValidateDownloadWithDiagnostics_Succeeds_WithCode()
    {
        TidalStreamService streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        TidalChunkDownloader downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        TidalDownloadClientSettings settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalDownloadClient client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>> result = await client.ValidateDownloadWithDiagnosticsAsync("t1", TidalQuality.Lossless);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("DL000", result.Value!["id"]);
        Assert.Equal("t1", result.Value!["trackId"]);
    }

    [Fact]
    public async Task ValidateDownloadWithDiagnostics_Fails_WithStableCode()
    {
        TidalStreamService streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        TidalChunkDownloader downloader = new TidalChunkDownloader(new HttpClient(new FailHandler()));
        TidalDownloadClientSettings settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalDownloadClient client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);

        Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>> result = await client.ValidateDownloadWithDiagnosticsAsync("t1", TidalQuality.Lossless);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DL001", result.Error!.Metadata["id"]);
        Assert.Equal("t1", result.Error!.Metadata["trackId"]);
    }
}
