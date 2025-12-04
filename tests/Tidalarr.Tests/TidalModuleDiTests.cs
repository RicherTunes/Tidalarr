using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Performance;
using Tidalarr.Core.Interfaces;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Caching;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Integration;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalModuleDiTests
{
    [Fact]
    public void RegistersExpectedServices_AndLifetimes()
    {
        ServiceCollection services = new ServiceCollection();
        TidalIndexerSettings indexerSettings = new TidalIndexerSettings { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        TidalDownloadClientSettings downloadSettings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);

        ServiceProvider provider = services.BuildServiceProvider();

        // API typed client
        Assert.NotNull(provider.GetRequiredService<TidalApiClient>());

        // Interfaces
        _ = Assert.IsType<TidalOAuthService>(provider.GetRequiredService<ITidalAuth>());
        _ = Assert.IsType<TidalApiClient>(provider.GetRequiredService<ITidalCore>());
        _ = Assert.IsType<TidalResponseCache>(provider.GetRequiredService<IStreamingResponseCache>());

        // Integration endpoints
        Assert.NotNull(provider.GetRequiredService<TidalIndexer>());
        Assert.NotNull(provider.GetRequiredService<TidalDownloadClient>());

        // Performance services
        IUniversalAdaptiveRateLimiter limiter = provider.GetRequiredService<IUniversalAdaptiveRateLimiter>();
        _ = Assert.IsType<TidalRateLimiter>(limiter);
    }
}


