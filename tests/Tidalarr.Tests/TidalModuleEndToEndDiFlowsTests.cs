using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalModuleEndToEndDiFlowsTests
{
    private class AuthStub : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("https://auth", "ver", "state", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Create());
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Create());
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Create());
        private static TidalTokens Create() => new("atk", "rtk", "Bearer", DateTime.UtcNow.AddHours(1), "sess1", "US", "user1");
    }

    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo(trackId, "Song", new List<string> { "Artist" }, "al1", "Album", 1, 120, TidalQuality.Lossless, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo(albumId, "Album", new List<string> { "Artist" }, new List<TidalTrackInfo>(), new List<TidalQuality> { TidalQuality.Lossless }, DateTime.UtcNow.Date, "cover", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo> { new TidalAlbumInfo("al1", "Album", new List<string> { "Artist" }, new List<TidalTrackInfo>(), new List<TidalQuality> { TidalQuality.Lossless }, DateTime.UtcNow.Date, "cover", true) },
                                                       new List<TidalTrackInfo> { new TidalTrackInfo("t1", "Song", new List<string> { "Artist" }, "al1", "Album", 1, 120, TidalQuality.Lossless, true, DateTime.UtcNow) },
                                                       2, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, new[] { "https://chunk/1" }, ".m4a", "application/vnd.tidal.bts", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) });
    }

    [Fact]
    public async Task DI_Flow_IndexerSearch_And_DownloadValidation_Work()
    {
        var indexerSettings = new TidalIndexerSettings { TidalMarket = "US", RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = "C:/temp", EnableCache = true, CacheDuration = 5 };
        var downloadSettings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };

        var services = new ServiceCollection();
        services.AddSingleton(indexerSettings);
        services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);

        // Override seams for deterministic behavior
        services.AddScoped<ITidalAuth, AuthStub>();
        services.AddScoped<ITidalCore, CoreStub>();
        services.AddScoped<TidalChunkDownloader>(_ => new TidalChunkDownloader(new HttpClient(new OkHandler())));
        services.AddScoped<TidalSearchService>(sp => new TidalSearchService(sp.GetRequiredService<ITidalCore>(), new Tidalarr.Domain.Quality.TidalQualityDetector()));

        var provider = services.BuildServiceProvider();
        var indexer = provider.GetRequiredService<TidalIndexer>();
        var downloader = provider.GetRequiredService<TidalDownloadClient>();

        var tracks = await indexer.SearchEnhancedAsync("queen");
        Assert.NotEmpty(tracks);

        var ok = await downloader.ValidateDownloadAsync("t1", TidalQuality.Lossless);
        Assert.True(ok);
    }
}





