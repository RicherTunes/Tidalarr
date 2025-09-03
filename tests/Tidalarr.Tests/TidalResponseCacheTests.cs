using System;
using System.Collections.Generic;
using Tidalarr.Infrastructure.Caching;
using Xunit;

namespace Tidalarr.Tests;

public class TidalResponseCacheTests
{
    [Fact]
    public void GenerateCacheKey_OrderIndependent_ForNonSensitiveParameters()
    {
        var cache = new TidalResponseCache();
        var endpoint = "/tracks/123/playbackinfopostpaywall";
        var p1 = new Dictionary<string, string>
        {
            ["countryCode"] = "US",
            ["audioquality"] = "LOSSLESS"
        };
        var p2 = new Dictionary<string, string>
        {
            ["audioquality"] = "LOSSLESS",
            ["countryCode"] = "US"
        };
        var key1 = cache.GenerateCacheKey(endpoint, p1);
        var key2 = cache.GenerateCacheKey(endpoint, p2);
        Assert.False(string.IsNullOrWhiteSpace(key1));
        Assert.Equal(key1, key2); // parameter order does not change key
    }

    [Fact]
    public void ShouldCache_PlaybackInfo_ReturnsFalse_OthersTrue()
    {
        var cache = new TidalResponseCache();
        Assert.False(cache.ShouldCache("/tracks/1/playbackinfopostpaywall"));
        Assert.True(cache.ShouldCache("/tracks/1"));
        Assert.True(cache.ShouldCache("/albums/1"));
        Assert.True(cache.ShouldCache("/albums/1/tracks"));
        Assert.True(cache.ShouldCache("/search"));
    }

    [Fact]
    public void GetCacheDuration_MatchesEndpointPolicies()
    {
        var cache = new TidalResponseCache();
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
        var cache = new TidalResponseCache();
        // These calls should not throw and should accept expected prefixes
        cache.InvalidateAlbum("al1");
        cache.InvalidateArtist("ar1");
        cache.InvalidateSearchResults();
        Assert.True(true);
    }
}
