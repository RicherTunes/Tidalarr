using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalModuleEndToEndDiFlowsTests
{
    private class AuthStub : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
        {
            return Task.FromResult(new TidalAuthUrl("https://auth", "ver", "state", string.Empty));
        }

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
        {
            return Task.FromResult(Create());
        }

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
        {
            return Task.FromResult(Create());
        }

        public Task<TidalTokens> GetValidTokensAsync()
        {
            return Task.FromResult(Create());
        }

        private static TidalTokens Create()
        {
            return new("atk", "rtk", "Bearer", DateTime.UtcNow.AddHours(1), "sess1", "US", "user1");
        }
    }

    private class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "Song", new List<string> { "Artist" }, "al1", "Album", 1, 120, TidalQuality.Lossless, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo(albumId, "Album", new List<string> { "Artist" }, new List<TidalTrackInfo>(), new List<TidalQuality> { TidalQuality.Lossless }, DateTime.UtcNow.Date, "cover", true));
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
            return Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo> { new TidalAlbumInfo("al1", "Album", new List<string> { "Artist" }, new List<TidalTrackInfo>(), new List<TidalQuality> { TidalQuality.Lossless }, DateTime.UtcNow.Date, "cover", true) },
                                                               new List<TidalTrackInfo> { new TidalTrackInfo("t1", "Song", new List<string> { "Artist" }, "al1", "Album", 1, 120, TidalQuality.Lossless, true, DateTime.UtcNow) },
                                                               2, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, ["https://chunk/1"], ".m4a", "application/vnd.tidal.bts", false, null));
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
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        }
    }

    [Fact]
    public async Task DI_Flow_IndexerSearch_And_DownloadValidation_Work()
    {
        TidalIndexerSettings indexerSettings = new() { TidalMarket = "US", RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = "C:/temp", EnableCache = true, CacheDuration = 5 };
        TidalDownloadClientSettings downloadSettings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };

        ServiceCollection services = new();
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);

        // Override seams for deterministic behavior
        _ = services.AddScoped<ITidalAuth, AuthStub>();
        _ = services.AddScoped<ITidalCore, CoreStub>();
        _ = services.AddScoped(_ => new TidalChunkDownloader(new HttpClient(new OkHandler())));
        _ = services.AddScoped(sp => new TidalSearchService(sp.GetRequiredService<ITidalCore>(), new Domain.Quality.TidalQualityDetector()));

        ServiceProvider provider = services.BuildServiceProvider();
        TidalIndexer indexer = provider.GetRequiredService<TidalIndexer>();
        TidalDownloadClient downloader = provider.GetRequiredService<TidalDownloadClient>();

        List<Lidarr.Plugin.Abstractions.Models.StreamingSearchResult> tracks = await indexer.SearchEnhancedAsync("queen");
        Assert.NotEmpty(tracks);

        bool ok = await downloader.ValidateDownloadAsync("t1", TidalQuality.Lossless);
        Assert.True(ok);
    }
}





