using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Performance;
using Tidalarr.Core.Interfaces;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Caching;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Integration;
using Xunit;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalModuleDiTests
{
    [Fact]
    public void RegistersExpectedServices_AndLifetimes()
    {
        var services = new ServiceCollection();
        var indexerSettings = new TidalIndexerSettings { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = System.IO.Path.GetTempPath() };
        var downloadSettings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = System.IO.Path.GetTempPath() };
        services.AddSingleton(indexerSettings);
        services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);

        var provider = services.BuildServiceProvider();

        // API typed client
        Assert.NotNull(provider.GetRequiredService<TidalApiClient>());

        // Interfaces
        Assert.IsType<TidalOAuthService>(provider.GetRequiredService<ITidalAuth>());
        Assert.IsType<TidalApiClient>(provider.GetRequiredService<ITidalCore>());
        Assert.IsType<TidalResponseCache>(provider.GetRequiredService<IStreamingResponseCache>());

        // Integration endpoints
        Assert.NotNull(provider.GetRequiredService<TidalIndexer>());
        Assert.NotNull(provider.GetRequiredService<TidalDownloadClient>());

        // Performance services
        var limiter = provider.GetRequiredService<IUniversalAdaptiveRateLimiter>();
        Assert.IsType<TidalRateLimiter>(limiter);
    }
}


