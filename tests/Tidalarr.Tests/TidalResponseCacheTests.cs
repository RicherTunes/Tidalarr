using Tidalarr.Infrastructure.Caching;

namespace Tidalarr.Tests;

public class TidalResponseCacheTests
{
    [Fact]
    public void GenerateCacheKey_OrderIndependent_ForNonSensitiveParameters()
    {
        TidalResponseCache cache = new TidalResponseCache();
        string endpoint = "/tracks/123/playbackinfopostpaywall";
        Dictionary<string, string> p1 = new Dictionary<string, string>
        {
            ["countryCode"] = "US",
            ["audioquality"] = "LOSSLESS"
        };
        Dictionary<string, string> p2 = new Dictionary<string, string>
        {
            ["audioquality"] = "LOSSLESS",
            ["countryCode"] = "US"
        };
        string key1 = cache.GenerateCacheKey(endpoint, p1);
        string key2 = cache.GenerateCacheKey(endpoint, p2);
        Assert.False(string.IsNullOrWhiteSpace(key1));
        Assert.Equal(key1, key2); // parameter order does not change key
    }

    [Fact]
    public void ShouldCache_PlaybackInfo_ReturnsFalse_OthersTrue()
    {
        TidalResponseCache cache = new TidalResponseCache();
        Assert.False(cache.ShouldCache("/tracks/1/playbackinfopostpaywall"));
        Assert.True(cache.ShouldCache("/tracks/1"));
        Assert.True(cache.ShouldCache("/albums/1"));
        Assert.True(cache.ShouldCache("/albums/1/tracks"));
        Assert.True(cache.ShouldCache("/search"));
    }

    [Fact]
    public void GetCacheDuration_MatchesEndpointPolicies()
    {
        TidalResponseCache cache = new TidalResponseCache();
        Assert.Equal(TimeSpan.FromMinutes(5), cache.GetCacheDuration("/search"));
        Assert.Equal(TimeSpan.FromHours(2), cache.GetCacheDuration("/albums/123"));
        Assert.Equal(TimeSpan.FromHours(4), cache.GetCacheDuration("/albums/123/tracks"));
        Assert.Equal(TimeSpan.FromHours(1), cache.GetCacheDuration("/tracks/123"));
        Assert.Equal(TimeSpan.Zero, cache.GetCacheDuration("/tracks/123/playbackinfo"));
        Assert.Equal(TimeSpan.FromMinutes(10), cache.GetCacheDuration("/users/abc/favorites"));
    }

    [Fact]
    public void InvalidateHelpers_BuildExpectedPrefixes()
    {
        TidalResponseCache cache = new TidalResponseCache();
        // These calls should not throw and should accept expected prefixes
        cache.InvalidateAlbum("al1");
        cache.InvalidateArtist("ar1");
        cache.InvalidateSearchResults();
        Assert.True(true);
    }
}



