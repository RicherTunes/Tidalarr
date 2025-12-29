using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Performance;
using Lidarr.Plugin.Common.Services.Network;
using Lidarr.Plugin.Common.Services.Registration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Caching;
using Lidarr.Plugin.Common.Services.Http;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Infrastructure.Storage;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration;

public class TidalModule : StreamingPluginModule
{
    public const string ModuleName = "Tidalarr";
    public new const string Version = "1.0.1";
    private static readonly string UserAgent = $"Tidalarr/{Version}";

    public override string ServiceName => "Tidal";
    public override string Description => "Tidal integration for Lidarr";
    public override string Author => "RicherTunes Community";

    public static void RegisterServices(IServiceCollection services)
    {
        new TidalModule().ConfigureServices(services);
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        RegisterSharedLibraryServices(services);
        _ = services.AddTransient<ContentDecodingSnifferHandler>();

        // Typed API client with OAuth delegating handler for transparent 401 refresh
        _ = services.AddHttpClient<TidalApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<ContentDecodingSnifferHandler>()
        .AddHttpMessageHandler(sp =>
        {
            IStreamingTokenProvider tokenProvider = sp.GetRequiredService<IStreamingTokenProvider>();
            Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            Microsoft.Extensions.Logging.ILogger logger = loggerFactory?.CreateLogger("OAuthDelegatingHandler") ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            return new OAuthDelegatingHandler(tokenProvider, logger);
        });

        _ = services.AddHttpClient<TidalOAuthService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<ContentDecodingSnifferHandler>();

        // Core services
        // PKCEGenerator is created internally by TidalOAuthService; no DI registration needed here.
        //
        // IMPORTANT: In Docker/plugin hosts, ApplicationData can resolve to a read-only location (e.g. /app/bin).
        // Always prefer the user-configured ConfigPath from settings for token persistence.
        _ = services.AddSingleton<ITokenStorage>(sp =>
        {
            var settings = sp.GetService<TidalarrSettings>();
            var configPath = settings?.ConfigPath;

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                var tokenPath = Path.Combine(configPath, "tidal_tokens.json");
                return new FileTokenStore(tokenPath);
            }

            // Dev/test fallback: avoid crashing when services are constructed before settings are provided.
            var fallbackPath = Path.Combine(Path.GetTempPath(), "Tidalarr", "tidal_tokens.json");
            return new FileTokenStore(fallbackPath);
        });
        _ = services.AddScoped<ITidalAuth, TidalOAuthService>();
        _ = services.AddSingleton<IStreamingAuthManager, TidalStreamingAuthManager>();
        // Token manager + provider
        _ = services.AddSingleton<IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials>>(sp => new TidalAuthTokenAuthAdapter(sp.GetRequiredService<ITidalAuth>()));
        _ = services.AddSingleton<StreamingTokenManager<TidalTokens, TidalCredentials>>();
        _ = services.AddSingleton<IStreamingTokenProvider, ManagedTokenProvider>();
        _ = services.AddScoped<ITidalCore, TidalApiClient>();

        // Shared-integrations
        _ = services.AddSingleton<TidalModelMapper>();
        _ = services.AddSingleton<TidalResponseCache>();
        _ = services.AddSingleton<TidalRateLimiter>();
        _ = services.AddSingleton<PerformanceMonitor>();

        // Domain services
        _ = services.AddScoped<TidalQualityDetector>();
        _ = services.AddScoped<TidalManifestParser>();
        _ = services.AddScoped<TidalStreamService>();
        _ = services.AddScoped<TidalChunkStreamProvider>();
        _ = services.AddScoped<IAudioStreamProvider>(sp => sp.GetRequiredService<TidalChunkStreamProvider>());

        // Application services
        _ = services.AddScoped<TidalSearchService>();

        // Back-compat: Map aggregated settings to distinct runtime settings if callers only registered TidalarrSettings
        services.TryAddSingleton(sp =>
        {
            TidalarrSettings? s = sp.GetService<TidalarrSettings>();
            return s is null
                ? new TidalIndexerSettings()
                : new TidalIndexerSettings
                {
                    BaseUrl = s.BaseUrl,
                    ConfigPath = s.ConfigPath,
                    RedirectUrl = s.RedirectUrl,
                    TidalMarket = s.TidalMarket,
                    EarlyReleaseLimit = s.EarlyReleaseLimit,
                    EnableCache = s.EnableCache,
                    CacheDuration = s.CacheDuration
                };
        });

        services.TryAddSingleton(sp =>
        {
            TidalarrSettings? s = sp.GetService<TidalarrSettings>();
            return s is null
                ? new TidalDownloadClientSettings()
                : new TidalDownloadClientSettings
                {
                    BaseUrl = s.BaseUrl,
                    PreferredQuality = s.PreferredQuality,
                    DownloadPath = s.DownloadPath,
                    IncludeMqa = s.IncludeMqa,
                    ExtractFlac = s.ExtractFlac,
                    ReEncodeAAC = s.ReEncodeAAC,
                    SaveSyncedLyrics = s.SaveSyncedLyrics,
                    UseLRCLIB = s.UseLRCLIB,
                    DownloadDelay = s.DownloadDelay,
                    DownloadDelayMin = s.DownloadDelayMin,
                    DownloadDelayMax = s.DownloadDelayMax
                };
        });

        // Integration endpoints
        _ = services.AddScoped<TidalIndexer>();
        _ = services.AddScoped<TidalDownloadClient>();

        // Orchestrator HttpClient (used only for direct-URL fallback; chunk path uses TidalChunkDownloader)
        _ = services.AddHttpClient("TidalOrchestrator", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<ContentDecodingSnifferHandler>();

        _ = services.AddHttpClient<TidalChunkDownloader>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<ContentDecodingSnifferHandler>();

    }



    private static void RegisterSharedLibraryServices(IServiceCollection services)
    {
        _ = services.AddSingleton<IStreamingResponseCache, TidalResponseCache>();
        _ = services.AddSingleton<IUniversalAdaptiveRateLimiter>(sp => sp.GetRequiredService<TidalRateLimiter>());
        _ = services.AddSingleton<PerformanceMonitor>();
        _ = services.AddSingleton<NetworkResilienceService>();
    }

    protected override void RegisterCoreServices()
    {
        // Reserved for future auto-registration via base module
    }

    public static TidalIndexer CreateIndexer(IServiceProvider serviceProvider, TidalIndexerSettings settings)
    {
        TidalModule module = new();
        ServiceProvider provider = module.BuildServiceProvider(settings);
        return provider.GetRequiredService<TidalIndexer>();
    }

    public static TidalDownloadClient CreateDownloadClient(IServiceProvider serviceProvider, TidalDownloadClientSettings settings)
    {
        TidalModule module = new();
        ServiceProvider provider = module.BuildServiceProvider(settings);
        return provider.GetRequiredService<TidalDownloadClient>();
    }

    public static bool ValidateConfiguration(TidalIndexerSettings settings)
    {
        return settings.IsValid(out _);
    }

    public static bool ValidateConfiguration(TidalDownloadClientSettings settings)
    {
        return settings.IsValid(out _);
    }

    // Convenience factory to produce a shared orchestrator wired to Tidal services
    public static SimpleDownloadOrchestrator CreateOrchestrator(IServiceProvider serviceProvider)
    {
        IHttpClientFactory httpFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpFactory.CreateClient("TidalOrchestrator");

        ITidalCore api = serviceProvider.GetRequiredService<ITidalCore>();
        TidalModelMapper mapper = serviceProvider.GetRequiredService<TidalModelMapper>();
        TidalStreamService streamService = serviceProvider.GetRequiredService<TidalStreamService>();
        TidalChunkStreamProvider chunkProvider = serviceProvider.GetRequiredService<TidalChunkStreamProvider>();

        // Delegates for orchestrator
        async Task<StreamingAlbum> getAlbum(string id)
        {
            return mapper.ToStreamingAlbum(await api.GetAlbumWithTracksAsync(id));
        }

        async Task<StreamingTrack> getTrack(string id)
        {
            return mapper.ToStreamingTrack(await api.GetTrackAsync(id));
        }

        async Task<IReadOnlyList<string>> getTrackIds(string id)
        {
            TidalAlbumInfo a = await api.GetAlbumWithTracksAsync(id);
            return a.Tracks?.Select(t => t.Id).ToList() ?? [];
        }
        async Task<(string Url, string Extension)> getStream(string id, StreamingQuality? q)
        {
            TidalQuality tidalQ = mapper.FromStreamingQuality(q ?? new StreamingQuality { Bitrate = 320 });
            TidalStreamInfo info = await api.GetStreamInfoAsync(id, tidalQ);
            string url = info.ChunkUrls?.FirstOrDefault() ?? string.Empty;
            string ext = info.FileExtension?.TrimStart('.') ?? "m4a";
            return (url, ext);
        }

        return new SimpleDownloadOrchestrator(
            serviceName: ModuleName,
            httpClient: httpClient,
            getAlbumAsync: getAlbum,
            getTrackAsync: getTrack,
            getAlbumTrackIdsAsync: getTrackIds,
            getStreamAsync: getStream,
            streamProvider: chunkProvider);
    }
}













