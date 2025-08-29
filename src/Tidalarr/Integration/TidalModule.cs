using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Services.Registration;
using Lidarr.Plugin.Common.Services.Performance;
using Lidarr.Plugin.Common.Services.Caching;
using Tidalarr.Infrastructure.Resilience;
using Tidalarr.Infrastructure.Telemetry;
using Tidalarr.Infrastructure.Caching;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Storage;
using Tidalarr.Application.Services;

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
        // Register shared library services first
        RegisterSharedLibraryServices(services);
        
        // HttpClient factory for proper HTTP client management
        services.AddHttpClient<TidalApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Tidalarr/1.0.0");
        });
        
        services.AddHttpClient<TidalOAuthService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        
        // Core services
        services.AddSingleton<PKCEGenerator>();
        services.AddSingleton<ITokenStorage, JsonTokenStorage>();
        services.AddScoped<ITidalAuth, TidalOAuthService>();
        services.AddScoped<ITidalCore, TidalApiClient>();
        
        // Shared library integrations
        services.AddSingleton<TidalModelMapper>();
        services.AddSingleton<TidalResponseCache>();
        services.AddSingleton<TidalRateLimiter>();
        services.AddSingleton<PerformanceMonitor>();
        
        // Domain services
        services.AddScoped<TidalQualityDetector>();
        services.AddScoped<TidalManifestParser>();
        services.AddScoped<TidalStreamService>();
        services.AddScoped<TidalChunkDownloader>();
        
        // Application services
        services.AddScoped<TidalSearchService>();
        
        // Integration services
        services.AddScoped<TidalIndexer>();
        services.AddScoped<TidalDownloadClient>();
    }
    
    private static void RegisterSharedLibraryServices(IServiceCollection services)
    {
        // Register shared library base services
        services.AddScoped<IStreamingResponseCache, TidalResponseCache>();
        services.AddScoped<AdaptiveRateLimiter, TidalRateLimiter>();
        services.AddTransient<PerformanceMonitor>();
    }
    
    protected override void RegisterCoreServices()
    {
        // This method is called by the base StreamingPluginModule
        // Register Tidalarr-specific services here
    }
    
    public static TidalIndexer CreateIndexer(IServiceProvider serviceProvider, TidalSettings settings)
    {
        // Register settings as singleton for this instance
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddLogging();
        RegisterServices(services);
        
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<TidalIndexer>();
    }
    
    public static TidalDownloadClient CreateDownloadClient(IServiceProvider serviceProvider, TidalSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddLogging();
        RegisterServices(services);
        
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<TidalDownloadClient>();
    }
    
    public static bool ValidateConfiguration(TidalSettings settings)
    {
        return settings.IsValid(out _);
    }
}
