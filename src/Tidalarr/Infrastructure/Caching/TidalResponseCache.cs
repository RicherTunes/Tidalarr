using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Observability;
using Lidarr.Plugin.Common.Services.Caching;

namespace Tidalarr.Infrastructure.Caching;

/// <summary>
/// Tidal-specific response cache implementation with endpoint-specific cache durations
/// </summary>
public class TidalResponseCache : StreamingResponseCache
{
    public TidalResponseCache(ILogger? logger = null) : base(logger!)
    {
        // Configure Tidal-specific cache settings
        DefaultCacheDuration = TimeSpan.FromMinutes(15);
        MaxCacheSize = 1000;
        CleanupInterval = TimeSpan.FromMinutes(5);
    }

    protected override string GetServiceName()
    {
        return "Tidal";
    }

    public override bool ShouldCache(string endpoint)
    {
        // Don't cache playback info as URLs are temporary
        return !endpoint.Contains("playbackinfo");
    }

    public override TimeSpan GetCacheDuration(string endpoint)
    {
        return endpoint.ToLowerInvariant() switch
        {
            // Search results - short cache for dynamic content
            _ when endpoint.Contains("/search") => TimeSpan.FromMinutes(5),

            // Albums - longer cache as they rarely change
            _ when endpoint.Contains("/albums/") && !endpoint.Contains("/tracks") => TimeSpan.FromHours(2),

            // Album tracks - long cache as tracklists are static
            _ when endpoint.Contains("/albums/") && endpoint.Contains("/tracks") => TimeSpan.FromHours(4),

            // Individual tracks - medium cache
            _ when endpoint.Contains("/tracks/") && !endpoint.Contains("playbackinfo") => TimeSpan.FromHours(1),

            // Artist info - long cache
            _ when endpoint.Contains("/artists/") => TimeSpan.FromHours(6),

            // User-specific content - short cache
            _ when endpoint.Contains("/users/") => TimeSpan.FromMinutes(10),

            // Playback info - never cache as URLs are temporary
            _ when endpoint.Contains("playbackinfo") => TimeSpan.Zero,

            // Default for other endpoints
            _ => DefaultCacheDuration
        };
    }

    // Use base class implementation for GenerateCacheKey with Dictionary parameters
    // Custom logic handled in GetServiceName() which provides the "tidal" prefix

    protected override bool ShouldFilterParameter(string parameterName, object parameterValue)
        => LogRedactor.IsSensitiveParameter(parameterName);

    protected override void OnCacheHit(string cacheKey)
    {
        Logger?.LogDebug("Tidal cache hit for key: {CacheKey}", SanitizeCacheKey(cacheKey));
    }

    protected override void OnCacheMiss(string cacheKey)
    {
        Logger?.LogDebug("Tidal cache miss for key: {CacheKey}", SanitizeCacheKey(cacheKey));
    }

    protected override void OnCacheEviction(string cacheKey, TimeSpan age)
    {
        Logger?.LogDebug("Tidal cache eviction for key: {CacheKey} (age: {Age})",
            SanitizeCacheKey(cacheKey), age);
    }

    private static string SanitizeCacheKey(string cacheKey)
    {
        // Remove sensitive information from log output
        return cacheKey.Contains("sessionId") || cacheKey.Contains("token")
            ? cacheKey.Length > 20 ? cacheKey[..20] + "..." : cacheKey
            : cacheKey;
    }

    /// <summary>
    /// Clear cache entries for a specific album (useful after album updates)
    /// </summary>
    public void InvalidateAlbum(string albumId)
    {
        InvalidateByPrefix($"tidal:/albums/{albumId}");
    }

    /// <summary>
    /// Clear cache entries for a specific artist (useful after artist updates)
    /// </summary>
    public void InvalidateArtist(string artistId)
    {
        InvalidateByPrefix($"tidal:/artists/{artistId}");
    }

    /// <summary>
    /// Clear all search results from cache
    /// </summary>
    public void InvalidateSearchResults()
    {
        InvalidateByPrefix("tidal:/search");
    }

}
