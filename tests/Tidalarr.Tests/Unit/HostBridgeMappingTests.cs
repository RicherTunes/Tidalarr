using Microsoft.Extensions.DependencyInjection;
using Tidalarr.HostBridge;
using Tidalarr.HostBridge.Settings;

namespace Tidalarr.Tests.Unit;

[Trait("scope", "cli")]
public class HostBridgeMappingTests
{
    [Fact]
    public void TidalarrHostSettings_ToCore_MapsAllFields()
    {
        TidalarrHostSettings host = new()
        {
            ConfigPath = "C:/cfg",
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            TidalMarket = "DE",
            EarlyReleaseLimit = 30,
            EnableCache = false,
            CacheDuration = 7,
            BaseUrl = "https://api.tidal.com"
        };

        Tidalarr.Integration.TidalarrSettings core = host.ToCore();
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
        TidalIndexerHostSettings host = new()
        {
            ConfigPath = "C:/cfg",
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            TidalMarket = "FR"
        };
        Tidalarr.Integration.TidalIndexerSettings core = host.ToCore();
        Assert.Equal(host.ConfigPath, core.ConfigPath);
        Assert.Equal(host.RedirectUrl, core.RedirectUrl);
        Assert.Equal(host.TidalMarket, core.TidalMarket);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_ToCore_MapsAllFieldsAndEnum()
    {
        TidalDownloadClientHostSettings host = new()
        {
            PreferredQuality = TidalQualityHost.HiRes,
            DownloadPath = "C:/out",
            DownloadDelay = 123,
            MaxConcurrentTrackDownloads = 2,
            MaxConcurrentChunkDownloads = 3
        };
        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();
        Assert.Equal(Core.Models.TidalQuality.HiRes, core.PreferredQuality);
        Assert.Equal(host.DownloadPath, core.DownloadPath);
        Assert.Equal(host.DownloadDelay, core.DownloadDelay);
        Assert.Equal(host.MaxConcurrentTrackDownloads, core.MaxConcurrentTrackDownloads);
        Assert.Equal(host.MaxConcurrentChunkDownloads, core.MaxConcurrentChunkDownloads);
    }

    [Fact]
    public void ServiceCollectionExtensions_RegistersMapper()
    {
        ServiceCollection services = new();
        _ = services.AddTidalarrHostBridgeServices();
        ServiceProvider provider = services.BuildServiceProvider();
        IHostSettingsMapper mapper = provider.GetRequiredService<IHostSettingsMapper>();
        Assert.NotNull(mapper);
    }

    // --- Edge-case / null / default tests (issue #4) ---

    [Fact]
    public void TidalarrHostSettings_NullInput_MapperReturnsDefaults()
    {
        HostSettingsMapper mapper = new();
        Tidalarr.Integration.TidalarrSettings core = mapper.ToCore((TidalarrHostSettings?)null);
        Assert.NotNull(core);
        Assert.Equal(string.Empty, core.ConfigPath);
        Assert.Equal(string.Empty, core.RedirectUrl);
        Assert.Equal("US", core.TidalMarket);
        Assert.Equal(14, core.EarlyReleaseLimit);
        Assert.True(core.EnableCache);
        Assert.Equal(15, core.CacheDuration);
    }

    [Fact]
    public void TidalIndexerHostSettings_NullInput_MapperReturnsDefaults()
    {
        HostSettingsMapper mapper = new();
        Tidalarr.Integration.TidalIndexerSettings core = mapper.ToCore((TidalIndexerHostSettings?)null);
        Assert.NotNull(core);
        Assert.Equal(string.Empty, core.ConfigPath);
        Assert.Equal(string.Empty, core.RedirectUrl);
        Assert.Equal("US", core.TidalMarket);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_NullInput_MapperReturnsDefaults()
    {
        HostSettingsMapper mapper = new();
        Tidalarr.Integration.TidalDownloadClientSettings core = mapper.ToCore((TidalDownloadClientHostSettings?)null);
        Assert.NotNull(core);
        Assert.Equal(Core.Models.TidalQuality.Lossless, core.PreferredQuality);
        Assert.Equal(string.Empty, core.DownloadPath);
        Assert.Equal(0, core.DownloadDelay);
        Assert.Equal(2, core.MaxConcurrentTrackDownloads);
        Assert.Equal(2, core.MaxConcurrentChunkDownloads);
    }

    [Fact]
    public void TidalarrHostSettings_DefaultConstructor_ProducesExpectedDefaults()
    {
        TidalarrHostSettings host = new();
        Tidalarr.Integration.TidalarrSettings core = host.ToCore();
        Assert.Equal(string.Empty, core.ConfigPath);
        Assert.Equal(string.Empty, core.RedirectUrl);
        Assert.Equal("US", core.TidalMarket);
        Assert.Equal(14, core.EarlyReleaseLimit);
        Assert.True(core.EnableCache);
        Assert.Equal(15, core.CacheDuration);
        Assert.Equal("https://api.tidal.com", core.BaseUrl);
    }

    [Fact]
    public void TidalarrHostSettings_NullEarlyReleaseLimit_MapsToNull()
    {
        TidalarrHostSettings host = new() { EarlyReleaseLimit = null };
        Tidalarr.Integration.TidalarrSettings core = host.ToCore();
        Assert.Null(core.EarlyReleaseLimit);
    }

    [Fact]
    public void TidalarrHostSettings_ZeroEarlyReleaseLimit_MapsToZero()
    {
        TidalarrHostSettings host = new() { EarlyReleaseLimit = 0 };
        Tidalarr.Integration.TidalarrSettings core = host.ToCore();
        Assert.Equal(0, core.EarlyReleaseLimit);
    }

    [Theory]
    [InlineData(TidalQualityHost.Low, Core.Models.TidalQuality.Low)]
    [InlineData(TidalQualityHost.High, Core.Models.TidalQuality.High)]
    [InlineData(TidalQualityHost.Lossless, Core.Models.TidalQuality.Lossless)]
    [InlineData(TidalQualityHost.HiRes, Core.Models.TidalQuality.HiRes)]
    public void TidalDownloadClientHostSettings_QualityEnum_MapsCorrectly(
        TidalQualityHost hostQuality, Core.Models.TidalQuality expectedQuality)
    {
        TidalDownloadClientHostSettings host = new() { PreferredQuality = hostQuality };
        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();
        Assert.Equal(expectedQuality, core.PreferredQuality);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_InvalidQualityEnum_FallsBackToLossless()
    {
        TidalDownloadClientHostSettings host = new() { PreferredQuality = (TidalQualityHost)999 };
        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();
        Assert.Equal(Core.Models.TidalQuality.Lossless, core.PreferredQuality);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_DefaultConstructor_ProducesExpectedDefaults()
    {
        TidalDownloadClientHostSettings host = new();
        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();
        Assert.Equal(Core.Models.TidalQuality.Lossless, core.PreferredQuality);
        Assert.Equal(string.Empty, core.DownloadPath);
        Assert.True(core.IncludeMqa);
        Assert.True(core.ExtractFlac);
        Assert.False(core.ReEncodeAAC);
        Assert.True(core.SaveSyncedLyrics);
        Assert.False(core.UseLRCLIB);
        Assert.Equal(0, core.DownloadDelay);
        Assert.Equal(2, core.MaxConcurrentTrackDownloads);
        Assert.Equal(2, core.MaxConcurrentChunkDownloads);
    }

    // --- Wave 4: Round-trip, extension, and additional edge-case tests ---

    [Fact]
    public void TidalDownloadClientHostSettings_ToCore_RoundTrip_MapsAllFields()
    {
        // Verify every field on DownloadClient host settings is faithfully mapped,
        // including boolean fields not covered by the original happy-path test.
        TidalDownloadClientHostSettings host = new()
        {
            PreferredQuality = TidalQualityHost.High,
            DownloadPath = "/music/tidal",
            IncludeMqa = false,
            ExtractFlac = false,
            ReEncodeAAC = true,
            SaveSyncedLyrics = false,
            UseLRCLIB = true,
            DownloadDelay = 500,
            MaxConcurrentTrackDownloads = 3,
            MaxConcurrentChunkDownloads = 4
        };

        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();

        Assert.Equal(Core.Models.TidalQuality.High, core.PreferredQuality);
        Assert.Equal("/music/tidal", core.DownloadPath);
        Assert.False(core.IncludeMqa);
        Assert.False(core.ExtractFlac);
        Assert.True(core.ReEncodeAAC);
        Assert.False(core.SaveSyncedLyrics);
        Assert.True(core.UseLRCLIB);
        Assert.Equal(500, core.DownloadDelay);
        Assert.Equal(3, core.MaxConcurrentTrackDownloads);
        Assert.Equal(4, core.MaxConcurrentChunkDownloads);
    }

    [Fact]
    public void TidalarrHostSettings_ToCore_RoundTrip_MapsAllEditableFields()
    {
        // Verify all settable fields round-trip faithfully.
        TidalarrHostSettings host = new()
        {
            ConfigPath = "/opt/tidalarr/config",
            RedirectUrl = "https://tidal.com/android/login/auth?code=abc&state=xyz",
            TidalMarket = "JP",
            EarlyReleaseLimit = 7,
            EnableCache = false,
            CacheDuration = 30,
            BaseUrl = "https://custom.tidal.api"
        };

        Tidalarr.Integration.TidalarrSettings core = host.ToCore();

        Assert.Equal("/opt/tidalarr/config", core.ConfigPath);
        Assert.Equal("https://tidal.com/android/login/auth?code=abc&state=xyz", core.RedirectUrl);
        Assert.Equal("JP", core.TidalMarket);
        Assert.Equal(7, core.EarlyReleaseLimit);
        Assert.False(core.EnableCache);
        Assert.Equal(30, core.CacheDuration);
        // Note: BaseUrl is NOT mapped by ToCore() — core uses its own default.
        // This is intentional: BaseUrl is host-only configuration.
    }

    [Fact]
    public void TidalIndexerHostSettings_DefaultConstructor_ProducesExpectedDefaults()
    {
        TidalIndexerHostSettings host = new();
        Tidalarr.Integration.TidalIndexerSettings core = host.ToCore();

        Assert.Equal(string.Empty, core.ConfigPath);
        Assert.Equal(string.Empty, core.RedirectUrl);
        Assert.Equal("US", core.TidalMarket);
    }

    [Fact]
    public void TidalIndexerHostSettings_ToCore_RoundTrip_MapsAllFields()
    {
        TidalIndexerHostSettings host = new()
        {
            ConfigPath = "/etc/tidalarr",
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=s",
            TidalMarket = "AU"
        };

        Tidalarr.Integration.TidalIndexerSettings core = host.ToCore();

        Assert.Equal("/etc/tidalarr", core.ConfigPath);
        Assert.Equal("https://tidal.com/android/login/auth?code=test&state=s", core.RedirectUrl);
        Assert.Equal("AU", core.TidalMarket);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_NegativeEnumValue_FallsBackToLossless()
    {
        TidalDownloadClientHostSettings host = new() { PreferredQuality = (TidalQualityHost)(-1) };
        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();
        Assert.Equal(Core.Models.TidalQuality.Lossless, core.PreferredQuality);
    }

    [Fact]
    public void TidalarrHostSettings_EmptyStrings_MapToEmptyStrings()
    {
        TidalarrHostSettings host = new()
        {
            ConfigPath = string.Empty,
            RedirectUrl = string.Empty,
            TidalMarket = string.Empty
        };

        Tidalarr.Integration.TidalarrSettings core = host.ToCore();

        Assert.Equal(string.Empty, core.ConfigPath);
        Assert.Equal(string.Empty, core.RedirectUrl);
        Assert.Equal(string.Empty, core.TidalMarket);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_EmptyDownloadPath_MapsToEmptyString()
    {
        TidalDownloadClientHostSettings host = new() { DownloadPath = string.Empty };
        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();
        Assert.Equal(string.Empty, core.DownloadPath);
    }

    [Fact]
    public void TidalDownloadClientHostSettings_ZeroConcurrency_MapsToZero()
    {
        TidalDownloadClientHostSettings host = new()
        {
            MaxConcurrentTrackDownloads = 0,
            MaxConcurrentChunkDownloads = 0,
            DownloadDelay = 0
        };

        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();

        Assert.Equal(0, core.MaxConcurrentTrackDownloads);
        Assert.Equal(0, core.MaxConcurrentChunkDownloads);
        Assert.Equal(0, core.DownloadDelay);
    }

    // --- ToCoreObject extension method tests ---

    [Fact]
    public void ToCoreObject_TidalarrHostSettings_ReturnsCorrectType()
    {
        HostSettingsMapper mapper = new();
        TidalarrHostSettings host = new() { TidalMarket = "DE" };

        object result = mapper.ToCoreObject(host);

        Tidalarr.Integration.TidalarrSettings core = Assert.IsType<Tidalarr.Integration.TidalarrSettings>(result);
        Assert.Equal("DE", core.TidalMarket);
    }

    [Fact]
    public void ToCoreObject_TidalIndexerHostSettings_ReturnsCorrectType()
    {
        HostSettingsMapper mapper = new();
        TidalIndexerHostSettings host = new() { TidalMarket = "FR" };

        object result = mapper.ToCoreObject(host);

        Tidalarr.Integration.TidalIndexerSettings core = Assert.IsType<Tidalarr.Integration.TidalIndexerSettings>(result);
        Assert.Equal("FR", core.TidalMarket);
    }

    [Fact]
    public void ToCoreObject_TidalDownloadClientHostSettings_ReturnsCorrectType()
    {
        HostSettingsMapper mapper = new();
        TidalDownloadClientHostSettings host = new() { PreferredQuality = TidalQualityHost.HiRes };

        object result = mapper.ToCoreObject(host);

        Tidalarr.Integration.TidalDownloadClientSettings core = Assert.IsType<Tidalarr.Integration.TidalDownloadClientSettings>(result);
        Assert.Equal(Core.Models.TidalQuality.HiRes, core.PreferredQuality);
    }

    [Fact]
    public void ToCoreObject_UnknownType_ReturnsInputUnchanged()
    {
        HostSettingsMapper mapper = new();
        string unknownSettings = "not a real settings object";

        object result = mapper.ToCoreObject(unknownSettings);

        Assert.Same(unknownSettings, result);
    }
}
