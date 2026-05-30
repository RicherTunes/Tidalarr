using System.Net;
using Lidarr.Plugin.Abstractions.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Hosting;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Bridge;
using Lidarr.Plugin.Common.Services.Performance;
using Lidarr.Plugin.Common.Services.Network;
using Lidarr.Plugin.Common.Services.Registration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Api;
using Tidalarr.Infrastructure.Resilience;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Caching;
using Lidarr.Plugin.Common.Services.Http;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Infrastructure.Storage;
using Tidalarr.Integration.LidarrNative;
using Lidarr.Plugin.Common.Extensions;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration;

public class TidalModule : StreamingPluginModule
{
    public const string ModuleName = "Tidalarr";

    private static int _hooksRegistered;

    // Version is derived from the assembly version (which Tidalarr.csproj wires up from the
    // top-level VERSION file via Directory.Build.props). Don't reintroduce a hardcoded literal —
    // it will drift the next time VERSION is bumped, as it did 1.0.1 → 1.1.0 → 1.1.1.
    public static readonly new string Version =
        typeof(TidalModule).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

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
        // TidalBackendHealthHandler gates connection-refused / DNS failures (network-down cascade).
        // It is independent of AuthFailureGate (which trips on 401/403). Both gates coexist:
        // BackendHealthCache short-circuits when the host is unreachable; AuthFailureGate
        // short-circuits when auth is latched bad. They never overlap because the failure
        // signals are disjoint (network-class vs HTTP-class).
        _ = services.AddTransient<TidalBackendHealthHandler>();
        // TidalRateLimitingHandler is the single global gate that prevents 429 storms when
        // Lidarr fans out searches/downloads across many artists. Every AddHttpClient below
        // chains it so chunk fetches, OAuth, search, and orchestrator calls all share one
        // budget against api.tidal.com / *.audio.tidal.com. See the handler's class doc for
        // backstory: the underlying TidalRateLimiter was previously dead code (registered in
        // DI, never invoked) — wiring this handler converts it into an actual ceiling.
        _ = services.AddTransient<Tidalarr.Infrastructure.Performance.TidalRateLimitingHandler>();

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
        .AddHttpMessageHandler<TidalBackendHealthHandler>()
        .AddHttpMessageHandler<Tidalarr.Infrastructure.Performance.TidalRateLimitingHandler>()
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
        .AddHttpMessageHandler<TidalBackendHealthHandler>()
        .AddHttpMessageHandler<Tidalarr.Infrastructure.Performance.TidalRateLimitingHandler>()
        .AddHttpMessageHandler<ContentDecodingSnifferHandler>();

        // Core services
        // PKCEGenerator is created internally by TidalOAuthService; no DI registration needed here.
        //
        // IMPORTANT: In Docker/plugin hosts, ApplicationData can resolve to a read-only location (e.g. /app/bin).
        // Always prefer the user-configured ConfigPath from settings for token persistence.
        //
        // Token persistence routes through common's encrypted FileTokenStore<TidalTokens>
        // (TokenProtectorFactory: DPAPI on Windows, Keychain on macOS, Secret Service on Linux,
        // DataProtection fallback). Legacy plaintext tidal_tokens.json files are migrated in-place
        // by LegacyTokenMigration before the store is first read.
        _ = services.AddSingleton<ITokenStore<TidalTokens>>(sp =>
        {
            TidalarrSettings? settings = sp.GetService<TidalarrSettings>();
            string? configPath = settings?.ConfigPath;

            if (string.IsNullOrWhiteSpace(configPath))
            {
                return new FailOnIOTokenStore<TidalTokens>();
            }

            string tokenPath = Path.Combine(configPath, "tidal_tokens.json");
            Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            Microsoft.Extensions.Logging.ILogger<Lidarr.Plugin.Common.Services.Authentication.FileTokenStore<TidalTokens>>? storeLogger =
                loggerFactory?.CreateLogger<Lidarr.Plugin.Common.Services.Authentication.FileTokenStore<TidalTokens>>();
            Lidarr.Plugin.Common.Services.Authentication.FileTokenStore<TidalTokens> store =
                new(tokenPath, serializerOptions: null, logger: storeLogger);

            // Best-effort one-shot migration of pre-Phase-2 plaintext files. Idempotent and safe to
            // call on every startup: it no-ops when no legacy file exists or when the file is already
            // in common's envelope format.
            Microsoft.Extensions.Logging.ILogger? migrationLogger = loggerFactory?.CreateLogger("Tidalarr.Infrastructure.Storage.LegacyTokenMigration");
            try
            {
                _ = Task.Run(() => LegacyTokenMigration.MigrateIfPresentAsync(configPath, store, migrationLogger)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                migrationLogger?.LogWarning(ex, "Legacy token migration failed; continuing without migration");
            }

            return store;
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
        // Singleton so the LRCLIB HttpClient (owned by LrclibClient) is reused across downloads and
        // disposed on container teardown. Injected into TidalAudioPostProcessor, which only invokes it
        // when SaveSyncedLyrics + UseLRCLIB are both enabled.
        _ = services.AddSingleton<ILyricsEnricher>(_ => new LyricsEnricher());
        _ = services.AddScoped<IAudioPostProcessor, TidalAudioPostProcessor>();
        _ = services.AddSingleton<IDownloadTelemetrySink, TidalDownloadTelemetrySink>();

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
                    MaxConcurrentTrackDownloads = s.MaxConcurrentTrackDownloads,
                    MaxConcurrentChunkDownloads = s.MaxConcurrentChunkDownloads
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
        .AddHttpMessageHandler<TidalBackendHealthHandler>()
        .AddHttpMessageHandler<Tidalarr.Infrastructure.Performance.TidalRateLimitingHandler>()
        .AddHttpMessageHandler<ContentDecodingSnifferHandler>();

        _ = services.AddHttpClient<TidalChunkDownloader>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<TidalBackendHealthHandler>()
        .AddHttpMessageHandler<Tidalarr.Infrastructure.Performance.TidalRateLimitingHandler>()
        .AddHttpMessageHandler<ContentDecodingSnifferHandler>();

        // Bridge runtime defaults — call LAST so plugins can override with custom implementations
        services.AddBridgeDefaults();

        // AuthFailureGate — singleton wrapping the IAuthFailureHandler registered by
        // AddBridgeDefaults above. Prevents Lidarr's search loop from hammering api.tidal.com
        // when credentials are known bad (the qobuzarr-incident class — user IP-banned after
        // session expired and Lidarr's search loop kept driving 401s at full rate).
        //
        // Mirrors the apple + qobuz wiring (AppleMusicarrStreamingPlugin.cs:130-134 and
        // QobuzarrStreamingPlugin.cs:36). The gate sits between the auth-state contract
        // (IAuthFailureHandler) and call-site short-circuit logic in TidalLidarrIndexer +
        // TidalLidarrDownloadClient: when latched bad, only one probe slot per 60s is granted
        // so a re-credentialed user can recover without spamming the upstream.
        services.AddSingleton(sp => new AuthFailureGate(
            sp.GetRequiredService<IAuthFailureHandler>(),
            TimeProvider.System,
            TimeSpan.FromSeconds(60),
            sp.GetService<ILogger<AuthFailureGate>>()));

        // Suppress Microsoft.Extensions.Http's MetricsFactoryHttpMessageHandlerFilter.
        //
        // AddHttpClient (called 4× above) auto-registers this filter via
        // TryAddEnumerable. The filter calls `socketsHandler.MeterFactory ??= ...` on
        // the primary handler. After ILRepack internalizes M.E.Http into the merged
        // plugin DLL and the plugin loads in an isolated AssemblyLoadContext, the JIT
        // lookup for `SocketsHttpHandler.get_MeterFactory` throws
        // MissingMethodException — the ALC's System.Net.Http resolution path produces
        // a metadata view in which that property reference can't be bound (despite
        // .NET 8 having the property on the BCL type). This breaks every
        // PluginSandboxRuntimeTests assertion that builds the DI graph (CI red since
        // 2026-03-28: Plugin_CreateDownloadClientAsync_*, Plugin_CreateIndexerAsync_*,
        // plus DockerE2E / IndexerCovTests downstream).
        //
        // We don't actually use HttpClient metrics — Lidarr surfaces its own. Removing
        // the filter post-AddHttpClient is the most surgical fix; we keep
        // IHttpClientFactory's connection pooling, typed-client wiring, and
        // delegating-handler composition intact. Targets only the named filter type
        // so future Logging / PolicyHttpMessageHandler filters added to M.E.Http are
        // unaffected.
        SuppressHttpClientMetricsFilter(services);
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

    protected override void RegisterCustomServices()
    {
        base.RegisterCustomServices();

        // CAS-guarded — only register the hooks once per process, even if RegisterServices()
        // is invoked multiple times (e.g. multiple module instances constructed in tests).
        if (System.Threading.Interlocked.CompareExchange(ref _hooksRegistered, 1, 0) != 0)
        {
            return;
        }

        // Tear down the two static runtime caches on plugin unload. Each holds an
        // IServiceProvider whose HttpClients would otherwise linger in the old ALC until GC.
        // ResetAsync is async; hop to thread pool to avoid deadlocking on captured-context dispose.
        PluginLifecycle.RegisterShutdown(
            "TidalIndexerRuntimeCache",
            static () =>
            {
                try { Task.Run(() => TidalIndexerRuntimeCache.Shared.ResetAsync()).GetAwaiter().GetResult(); }
                catch { /* teardown errors are not actionable */ }
            });
        PluginLifecycle.RegisterShutdown(
            "TidalDownloadClientRuntimeCache",
            static () =>
            {
                try { Task.Run(() => TidalDownloadClientRuntimeCache.Shared.ResetAsync()).GetAwaiter().GetResult(); }
                catch { /* teardown errors are not actionable */ }
            });
    }

    public override void Dispose()
    {
        base.Dispose();
        PluginLifecycle.Shutdown();
        // Reset the hook-registration guard so a subsequent module instance can re-register.
        System.Threading.Interlocked.Exchange(ref _hooksRegistered, 0);
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
        IAudioPostProcessor? postProcessor = serviceProvider.GetService<IAudioPostProcessor>();
        IDownloadTelemetrySink? telemetrySink = serviceProvider.GetService<IDownloadTelemetrySink>();
        TidalDownloadClientSettings? downloadSettings = serviceProvider.GetService<TidalDownloadClientSettings>();
        int maxConcurrentTracks = downloadSettings?.MaxConcurrentTrackDownloads ?? 1;

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
            maxConcurrentTracks: maxConcurrentTracks,
            streamProvider: chunkProvider,
            metadataApplier: null,
            logger: null,
            postProcessor: postProcessor,
            telemetrySink: telemetrySink);
    }

    /// <summary>
    /// Removes <c>MetricsFactoryHttpMessageHandlerFilter</c> from the service collection
    /// to prevent the cross-ALC <c>SocketsHttpHandler.MeterFactory</c> resolution failure.
    /// See the comment at the top of <see cref="ConfigureServices"/> for the full backstory.
    /// Resolved by reflection because the filter type is internal to M.E.Http.
    /// </summary>
    private static void SuppressHttpClientMetricsFilter(IServiceCollection services)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            System.Type? implType = services[i].ImplementationType;
            if (implType is not null && implType.FullName == "Microsoft.Extensions.Http.MetricsFactoryHttpMessageHandlerFilter")
            {
                services.RemoveAt(i);
            }
        }
    }
}








