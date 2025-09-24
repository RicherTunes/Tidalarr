using System.Net.Http;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Caching;
using Lidarr.Plugin.Common.Services.Performance;
using Lidarr.Plugin.Common.Services.Network;
using Lidarr.Plugin.Common.Services.Registration;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Caching;
using Tidalarr.Infrastructure.Http;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Infrastructure.Storage;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Common.Models;

namespace Tidalarr.Integration;

public class TidalModule : StreamingPluginModule
{
    public const string ModuleName = "Tidalarr";
    public new const string Version = "1.0.0";

    public override string ServiceName => "Tidal";
    public override string Description => "Tidal integration for Lidarr";
    public override string Author => "RicherTunes Community";

    public static void RegisterServices(IServiceCollection services)
    {
        RegisterSharedLibraryServices(services);
        services.AddTransient<GzipSniffingHandler>();
        services.AddTransient<WiretapDiagnosticHandler>();

        // Typed API client with OAuth delegating handler for transparent 401 refresh
        services.AddHttpClient<TidalApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Tidalarr/1.0.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<GzipSniffingHandler>()
        .AddHttpMessageHandler<WiretapDiagnosticHandler>()
        .AddHttpMessageHandler(sp =>
        {
            var tokenProvider = sp.GetRequiredService<IStreamingTokenProvider>();
            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("OAuthDelegatingHandler") ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            return new Lidarr.Plugin.Common.Services.Http.OAuthDelegatingHandler(tokenProvider, logger);
        });

        services.AddHttpClient<TidalOAuthService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<GzipSniffingHandler>()
        .AddHttpMessageHandler<WiretapDiagnosticHandler>();

        // Core services
        services.AddSingleton<PKCEGenerator>();
        services.AddSingleton<ITokenStorage, JsonTokenStorage>();
        services.AddScoped<ITidalAuth, TidalOAuthService>();
        // Expose as shared token provider for OAuth handler; adapt stubs if needed
        services.AddScoped<IStreamingTokenProvider>(sp =>
        {
            var auth = sp.GetRequiredService<ITidalAuth>();
            if (auth is IStreamingTokenProvider tp) return tp;
            return new OAuthTokenProviderAdapter(auth);
        });
        services.AddScoped<ITidalCore, TidalApiClient>();

        // Shared-integrations
        services.AddSingleton<TidalModelMapper>();
        services.AddSingleton<TidalResponseCache>();
        services.AddSingleton<TidalRateLimiter>();
        services.AddSingleton<PerformanceMonitor>();

        // Domain services
        services.AddScoped<TidalQualityDetector>();
        services.AddScoped<TidalManifestParser>();
        services.AddScoped<TidalStreamService>();
        services.AddScoped<TidalChunkStreamProvider>();
        services.AddScoped<IAudioStreamProvider>(sp => sp.GetRequiredService<TidalChunkStreamProvider>());

        // Application services
        services.AddScoped<TidalSearchService>();

        // Integration endpoints
        services.AddScoped<TidalIndexer>();
        services.AddScoped<TidalDownloadClient>();

        // Orchestrator HttpClient (used only for direct-URL fallback; chunk path uses TidalChunkDownloader)
        services.AddHttpClient("TidalOrchestrator", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Tidalarr/1.0.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<GzipSniffingHandler>()
        .AddHttpMessageHandler<WiretapDiagnosticHandler>();

        services.AddHttpClient<TidalChunkDownloader>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        .AddHttpMessageHandler<GzipSniffingHandler>()
        .AddHttpMessageHandler<WiretapDiagnosticHandler>();
    }

    private static void RegisterSharedLibraryServices(IServiceCollection services)
    {
        services.AddSingleton<IStreamingResponseCache, TidalResponseCache>();
        services.AddSingleton<AdaptiveRateLimiter, TidalRateLimiter>();
        services.AddSingleton<PerformanceMonitor>();
        services.AddSingleton<NetworkResilienceService>();
    }

    protected override void RegisterCoreServices()
    {
        // Reserved for future auto-registration via base module
    }

    public static TidalIndexer CreateIndexer(IServiceProvider serviceProvider, TidalIndexerSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        RegisterServices(services);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<TidalIndexer>();
    }

    public static TidalDownloadClient CreateDownloadClient(IServiceProvider serviceProvider, TidalDownloadSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        RegisterServices(services);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<TidalDownloadClient>();
    }

    public static bool ValidateConfiguration(TidalIndexerSettings settings) => settings.IsValid(out _);
    public static bool ValidateConfiguration(TidalDownloadSettings settings) => settings.IsValid(out _);

    // Convenience factory to produce a shared orchestrator wired to Tidal services
    public static SimpleDownloadOrchestrator CreateOrchestrator(IServiceProvider serviceProvider)
    {
        var httpFactory = serviceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
        var httpClient = httpFactory.CreateClient("TidalOrchestrator");

        var api = serviceProvider.GetRequiredService<ITidalCore>();
        var mapper = serviceProvider.GetRequiredService<TidalModelMapper>();
        var streamService = serviceProvider.GetRequiredService<TidalStreamService>();
        var chunkProvider = serviceProvider.GetRequiredService<TidalChunkStreamProvider>();

        // Delegates for orchestrator
        Func<string, Task<StreamingAlbum>> getAlbum = async id => mapper.ToStreamingAlbum(await api.GetAlbumWithTracksAsync(id));
        Func<string, Task<StreamingTrack>> getTrack = async id => mapper.ToStreamingTrack(await api.GetTrackAsync(id));
        Func<string, Task<IReadOnlyList<string>>> getTrackIds = async id =>
        {
            var a = await api.GetAlbumWithTracksAsync(id);
            return (IReadOnlyList<string>)(a.Tracks?.Select(t => t.Id).ToList() ?? new List<string>());
        };
        Func<string, StreamingQuality?, Task<(string Url, string Extension)>> getStream = async (id, q) =>
        {
            var tidalQ = mapper.FromStreamingQuality(q ?? new StreamingQuality { Bitrate = 320 });
            var info = await api.GetStreamInfoAsync(id, tidalQ);
            var url = info.ChunkUrls?.FirstOrDefault() ?? string.Empty;
            var ext = info.FileExtension?.TrimStart('.') ?? "m4a";
            return (url, ext);
        };

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









