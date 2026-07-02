using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Observability;
using Lidarr.Plugin.Common.Services.Caching;

namespace Tidalarr.Infrastructure.Caching;

/// <summary>
/// Tidal-specific response cache implementation with endpoint-specific cache durations
/// </summary>
public class TidalResponseCache : StreamingResponseCache
{
    private readonly bool _enableCache;
    private readonly TimeSpan _searchCacheDuration;

    /// <summary>
    /// </summary>
    /// <param name="logger">Optional logger for cache hit/miss/eviction diagnostics.</param>
    /// <param name="enableCache">
    /// Master toggle. When <c>false</c>, every endpoint reports <see cref="ShouldCache"/> = false —
    /// wires the indexer's "Enable Cache" setting (<see cref="Tidalarr.Integration.TidalIndexerSettings.EnableCache"/>),
    /// which previously had no runtime effect (T-3).
    /// </param>
    /// <param name="searchCacheDuration">
    /// TTL applied to <c>/search</c> endpoint results. Defaults to 5 minutes when not supplied.
    /// Wires the indexer's "Cache Duration" setting
    /// (<see cref="Tidalarr.Integration.TidalIndexerSettings.CacheDuration"/>), which previously had
    /// no runtime effect (T-3) — search caching always used a hardcoded 5-minute TTL regardless of
    /// user configuration.
    /// </param>
    public TidalResponseCache(ILogger? logger = null, bool enableCache = true, TimeSpan? searchCacheDuration = null) : base(logger!)
    {
        _enableCache = enableCache;
        _searchCacheDuration = searchCacheDuration ?? TimeSpan.FromMinutes(5);

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
        // Master "Enable Cache" toggle, then never cache playback info as URLs are temporary.
        return _enableCache && !endpoint.Contains("playbackinfo");
    }

    public override TimeSpan GetCacheDuration(string endpoint)
    {
        return endpoint.ToLowerInvariant() switch
        {
            // Search results - user-configurable via the "Cache Duration" setting
            _ when endpoint.Contains("/search") => _searchCacheDuration,

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
