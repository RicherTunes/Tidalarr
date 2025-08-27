using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Interfaces;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Storage;
using Tidalarr.Application.Services;

namespace Tidalarr.Integration;

public class TidalModule
{
    public const string ModuleName = "Tidalarr";
    public const string Version = "1.0.0";
    public const string Description = "Tidal integration for Lidarr";
    
    public static void RegisterServices(IServiceCollection services)
    {
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
    
    public static TidalIndexer CreateIndexer(IServiceProvider serviceProvider, TidalSettings settings)
    {
        // Register settings as singleton for this instance
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        RegisterServices(services);
        
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<TidalIndexer>();
    }
    
    public static TidalDownloadClient CreateDownloadClient(IServiceProvider serviceProvider, TidalSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        RegisterServices(services);
        
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<TidalDownloadClient>();
    }
    
    public static bool ValidateConfiguration(TidalSettings settings)
    {
        return settings.IsValid(out _);
    }
}
