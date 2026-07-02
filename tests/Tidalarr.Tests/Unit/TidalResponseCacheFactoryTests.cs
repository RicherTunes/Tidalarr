using Tidalarr.Infrastructure.Caching;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// T-3 (dead-settings audit): "Enable Cache" and "Cache Duration" were accepted, validated, and
/// copied between settings objects but the cache always used its hardcoded defaults regardless of
/// what the user configured. These tests pin <see cref="TidalResponseCacheFactory"/>, which gives
/// both settings a real effect on the <see cref="TidalResponseCache"/> the plugin actually uses.
/// </summary>
public class TidalResponseCacheFactoryTests
{
    [Fact]
    public void Create_WithNullSettings_UsesSafeDefaults()
    {
        TidalResponseCache cache = TidalResponseCacheFactory.Create(settings: null);

        Assert.True(cache.ShouldCache("/search"));
        Assert.Equal(TimeSpan.FromMinutes(15), cache.GetCacheDuration("/search"));
    }

    [Fact]
    public void Create_WithEnableCacheFalse_DisablesCachingForEveryEndpoint()
    {
        TidalIndexerSettings settings = new() { EnableCache = false, CacheDuration = 15 };

        TidalResponseCache cache = TidalResponseCacheFactory.Create(settings);

        Assert.False(cache.ShouldCache("/search"));
        Assert.False(cache.ShouldCache("/albums/1"));
        Assert.False(cache.ShouldCache("/tracks/1"));
    }

    [Fact]
    public void Create_WithEnableCacheTrue_StillNeverCachesPlaybackInfo()
    {
        TidalIndexerSettings settings = new() { EnableCache = true, CacheDuration = 15 };

        TidalResponseCache cache = TidalResponseCacheFactory.Create(settings);

        Assert.False(cache.ShouldCache("/tracks/1/playbackinfopostpaywall"));
        Assert.True(cache.ShouldCache("/search"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(0)]
    public void Create_ConfiguresSearchCacheDuration_FromCacheDurationSetting(int cacheDurationMinutes)
    {
        TidalIndexerSettings settings = new() { EnableCache = true, CacheDuration = cacheDurationMinutes };

        TidalResponseCache cache = TidalResponseCacheFactory.Create(settings);

        Assert.Equal(TimeSpan.FromMinutes(cacheDurationMinutes), cache.GetCacheDuration("/search"));
    }

    [Fact]
    public void Create_DoesNotChangeOtherEndpointPolicies()
    {
        TidalIndexerSettings settings = new() { EnableCache = true, CacheDuration = 60 };

        TidalResponseCache cache = TidalResponseCacheFactory.Create(settings);

        // Non-search endpoint durations are untouched by the Cache Duration setting.
        Assert.Equal(TimeSpan.FromHours(2), cache.GetCacheDuration("/albums/123"));
        Assert.Equal(TimeSpan.Zero, cache.GetCacheDuration("/tracks/123/playbackinfo"));
    }
}
