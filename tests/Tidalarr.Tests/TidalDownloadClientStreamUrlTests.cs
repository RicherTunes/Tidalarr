using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalDownloadClientStreamUrlTests
{
    private class ExposedDownloadClient : TidalDownloadClient
    {
        public ExposedDownloadClient(TidalStreamService streamService, TidalChunkDownloader chunkDownloader, ITidalCore apiClient, Tidalarr.Domain.Quality.TidalQualityDetector qualityDetector, TidalDownloadClientSettings settings)
            : base(streamService, chunkDownloader, apiClient, qualityDetector, settings, NullLogger.Instance) {}
        public Task<string> ExposeGetStreamUrlAsync(string trackId, string quality) => base.GetStreamUrlAsync(trackId, quality);
    }
    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo(trackId,"Song",new(){"Artist"},"al1","Album",1,100,TidalQuality.High,true,DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("","",new(),new(),new(),DateTime.MinValue,"",true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, new[]{"https://first","https://second"}, ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    [Fact]
    public async Task GetStreamUrlAsync_ReturnsFirstChunkUrl()
    {
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        var streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        var client = new ExposedDownloadClient(streamSvc, new TidalChunkDownloader(new HttpClient()), new CoreStub(), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings);

        var url = await client.ExposeGetStreamUrlAsync("t1", "LOSSLESS");
        Assert.Equal("https://first", url);
    }
}




