using Microsoft.Extensions.DependencyInjection;
using Tidalarr.HostBridge;
using Tidalarr.HostBridge.Settings;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class HostBridgeMappingTests
{
    [Fact]
    public void TidalarrHostSettings_ToCore_MapsAllFields()
    {
        var host = new TidalarrHostSettings
        {
            ConfigPath = "C:/cfg",
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            TidalMarket = "DE",
            EarlyReleaseLimit = 30,
            EnableCache = false,
            CacheDuration = 7,
            BaseUrl = "https://api.tidal.com"
        };

        var core = host.ToCore();
        Assert.Equal(host.ConfigPath, core.ConfigPath);
        Assert.Equal(host.RedirectUrl, core.RedirectUrl);
        Assert.Equal(host.TidalMarket, core.TidalMarket);
        Assert.Equal(host.EarlyReleaseLimit, core.EarlyReleaseLimit);
        Assert.Equal(host.EnableCache, core.EnableCache);
        Assert.Equal(host.CacheDuration, core.CacheDuration);
        Assert.Equal(host.BaseUrl, core.BaseUrl);
    }

    [Fact]
    public void TidalIndexerHostSettings_ToCore_MapsAllFields()
    {
        var host = new TidalIndexerHostSettings
        {
            ConfigPath = "C:/cfg",
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            TidalMarket = "FR"
        };
        var core = host.ToCore();
        Assert.Equal(host.ConfigPath, core.ConfigPath);
        Assert.Equal(host.RedirectUrl, core.RedirectUrl);
        Assert.Equal(host.TidalMarket, core.TidalMarket);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_ToCore_MapsAllFieldsAndEnum()
    {
        var host = new TidalDownloadClientHostSettings
        {
            PreferredQuality = TidalQualityHost.HiRes,
            DownloadPath = "C:/out",
            DownloadDelay = 123,
            DownloadDelayMin = 100,
            DownloadDelayMax = 200
        };
        var core = host.ToCore();
        Assert.Equal(Tidalarr.Core.Models.TidalQuality.HiRes, core.PreferredQuality);
        Assert.Equal(host.DownloadPath, core.DownloadPath);
        Assert.Equal(host.DownloadDelay, core.DownloadDelay);
        Assert.Equal(host.DownloadDelayMin, core.DownloadDelayMin);
        Assert.Equal(host.DownloadDelayMax, core.DownloadDelayMax);
    }

    [Fact]
    public void ServiceCollectionExtensions_RegistersMapper()
    {
        var services = new ServiceCollection();
        services.AddTidalarrHostBridgeServices();
        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IHostSettingsMapper>();
        Assert.NotNull(mapper);
    }
}

