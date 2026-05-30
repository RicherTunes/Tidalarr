using System.Net;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Common.Services.Performance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Caching;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

/// <summary>
/// Tech-debt wave 12: targeted coverage gap-fill for under-tested classes.
/// Real-numbers picks (from coverlet cobertura, HEAD d3645a0):
/// - TidalRateLimiter:           9.67% line-rate
/// - TidalDownloadTelemetrySink: 0%    line-rate
/// - TidalCredentials:           10%   line-rate
/// - TidalResponseCache:         52.7% (fill remaining branches)
/// Note: ObservabilityShim tests have been moved to Unit/ObservabilityShimTests.cs and
/// rewritten against LoggerExtensions directly (shim deleted after Mission #37).
/// </summary>
public class Wave12CoverageGapTests
{
    // ===== TidalRateLimiter =====

    [Fact]
    public void RateLimiter_NormalizesEmptyService_ToTidal()
    {
        using TidalRateLimiter limiter = new();
        // GetCurrentLimit through both empty + null services should return same as "Tidal"
        int empty = limiter.GetCurrentLimit(string.Empty, "/api/v1/search");
        int explicit_ = limiter.GetCurrentLimit("Tidal", "/api/v1/search");
        Assert.Equal(explicit_, empty);
    }

    [Fact]
    public void RateLimiter_NormalizesNullService_ToTidal()
    {
        using TidalRateLimiter limiter = new();
        int nullSvc = limiter.GetCurrentLimit(null!, "/api/v1/search");
        int tidal = limiter.GetCurrentLimit("Tidal", "/api/v1/search");
        Assert.Equal(tidal, nullSvc);
    }

    [Fact]
    public async Task RateLimiter_WaitIfNeeded_ServiceOverload_DefaultsToTidal()
    {
        using TidalRateLimiter limiter = new(NullLogger<TidalRateLimiter>.Instance);
        bool result = await limiter.WaitIfNeededAsync("/api/v1/tracks/1");
        Assert.True(result);
    }

    [Fact]
    public async Task RateLimiter_WaitIfNeeded_AcceptsCancellationToken()
    {
        using TidalRateLimiter limiter = new();
        using CancellationTokenSource cts = new();
        bool result = await limiter.WaitIfNeededAsync("Tidal", "/api/v1/albums/2", cts.Token);
        Assert.True(result);
    }

    [Fact]
    public async Task RateLimiter_WaitIfNeeded_NullEndpoint_DoesNotThrow()
    {
        using TidalRateLimiter limiter = new();
        // Should normalize null endpoint to empty string
        bool result = await limiter.WaitIfNeededAsync("Tidal", null!);
        Assert.True(result);
    }

    [Fact]
    public void RateLimiter_RecordResponse_NullEndpoint_DoesNotThrow()
    {
        using TidalRateLimiter limiter = new();
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        limiter.RecordResponse("Tidal", null!, response);
        // Should not throw; verify limiter remains usable
        Assert.True(limiter.GetCurrentLimit("Tidal", "/x") >= 0);
    }

    [Fact]
    public void RateLimiter_RecordResponse_AfterDispose_NoOps()
    {
        TidalRateLimiter limiter = new();
        limiter.Dispose();
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        // Per spec, RecordResponse silently no-ops after dispose (does not throw)
        Exception? ex = Record.Exception(() => limiter.RecordResponse("Tidal", "/x", response));
        Assert.Null(ex);
    }

    [Fact]
    public void RateLimiter_GetCurrentLimit_AfterDispose_Throws()
    {
        TidalRateLimiter limiter = new();
        limiter.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(() => limiter.GetCurrentLimit("Tidal", "/x"));
    }

    [Fact]
    public async Task RateLimiter_WaitIfNeeded_AfterDispose_Throws()
    {
        TidalRateLimiter limiter = new();
        limiter.Dispose();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await limiter.WaitIfNeededAsync("Tidal", "/x"));
    }

    [Fact]
    public void RateLimiter_GetTidalStats_ReturnsTidalServiceName()
    {
        using TidalRateLimiter limiter = new();
        ServiceRateLimitStats stats = limiter.GetTidalStats();
        Assert.NotNull(stats);
        Assert.Equal("Tidal", stats.ServiceName);
    }

    [Fact]
    public void RateLimiter_GetServiceStats_AfterDispose_Throws()
    {
        TidalRateLimiter limiter = new();
        limiter.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(() => limiter.GetServiceStats("Tidal"));
    }

    [Fact]
    public void RateLimiter_GetGlobalStats_AfterDispose_Throws()
    {
        TidalRateLimiter limiter = new();
        limiter.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(() => limiter.GetGlobalStats());
    }

    [Fact]
    public void RateLimiter_DoubleDispose_DoesNotThrow()
    {
        TidalRateLimiter limiter = new();
        limiter.Dispose();
        Exception? ex = Record.Exception(() => limiter.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void RateLimiter_RecordResponse_OkResponse_DoesNotThrow_AndStatsAccessible()
    {
        using TidalRateLimiter limiter = new();
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        limiter.RecordResponse("Tidal", "/api/v1/x", response);
        ServiceRateLimitStats stats = limiter.GetTidalStats();
        Assert.NotNull(stats);
        Assert.Equal("Tidal", stats.ServiceName);
    }

    [Fact]
    public void RateLimiter_RecordResponse_TooManyRequests_DoesNotThrow()
    {
        using TidalRateLimiter limiter = new();
        using HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
        Exception? ex = Record.Exception(() => limiter.RecordResponse("Tidal", "/api/v1/x", response));
        Assert.Null(ex);
    }

    [Fact]
    public void RateLimiter_RecordResponse_ServerError_DoesNotThrow()
    {
        using TidalRateLimiter limiter = new();
        using HttpResponseMessage response = new(HttpStatusCode.InternalServerError);
        Exception? ex = Record.Exception(() => limiter.RecordResponse("Tidal", "/api/v1/x", response));
        Assert.Null(ex);
    }

    [Fact]
    public void RateLimiter_GetGlobalStats_ReturnsNonNull()
    {
        using TidalRateLimiter limiter = new();
        GlobalRateLimitStats stats = limiter.GetGlobalStats();
        Assert.NotNull(stats);
    }

    // ===== TidalDownloadTelemetrySink =====
    // Removed: the plugin-local TidalDownloadTelemetrySink was consolidated into Common's
    // LoggingDownloadTelemetrySink (registered via AddDownloadTelemetry). Its best-effort
    // no-throw behavior is now covered by Common's DownloadTelemetryEnrichmentTests
    // (LoggingSink_* + legacy-record) and DownloadTelemetryService's try/catch.

    // ===== TidalCredentials =====

    [Fact]
    public void Credentials_Type_IsOAuth2()
    {
        TidalCredentials creds = new("https://example.com/cb");
        Assert.Equal(Lidarr.Plugin.Common.Interfaces.AuthenticationType.OAuth2, creds.Type);
    }

    [Fact]
    public void Credentials_IsValid_ValidHttpsUrl_ReturnsTrue()
    {
        TidalCredentials creds = new("https://tidal.com/callback");
        bool valid = creds.IsValid(out string err);
        Assert.True(valid);
        Assert.Equal(string.Empty, err);
    }

    [Fact]
    public void Credentials_IsValid_EmptyUrl_ReturnsFalse()
    {
        TidalCredentials creds = new(string.Empty);
        bool valid = creds.IsValid(out string err);
        Assert.False(valid);
        Assert.Contains("Redirect URL is required", err);
    }

    [Fact]
    public void Credentials_IsValid_WhitespaceUrl_ReturnsFalse()
    {
        TidalCredentials creds = new("   ");
        bool valid = creds.IsValid(out string err);
        Assert.False(valid);
        Assert.Contains("Redirect URL is required", err);
    }

    [Fact]
    public void Credentials_IsValid_RelativeUrl_ReturnsFalse()
    {
        TidalCredentials creds = new("/callback");
        bool valid = creds.IsValid(out string err);
        Assert.False(valid);
        Assert.Contains("absolute URL", err);
    }

    [Fact]
    public void Credentials_IsValid_MalformedUrl_ReturnsFalse()
    {
        TidalCredentials creds = new("not a url at all");
        bool valid = creds.IsValid(out string err);
        Assert.False(valid);
        Assert.Contains("absolute URL", err);
    }

    [Fact]
    public void Credentials_RecordEquality_SameUrl_AreEqual()
    {
        TidalCredentials a = new("https://example.com/cb");
        TidalCredentials b = new("https://example.com/cb");
        Assert.Equal(a, b);
    }

    // ===== TidalResponseCache: fill remaining gaps =====

    [Fact]
    public void Cache_GetCacheDuration_ArtistEndpoint_ReturnsSixHours()
    {
        TidalResponseCache cache = new();
        Assert.Equal(TimeSpan.FromHours(6), cache.GetCacheDuration("/artists/42"));
    }

    [Fact]
    public void Cache_GetCacheDuration_UnknownEndpoint_ReturnsDefault15Min()
    {
        TidalResponseCache cache = new();
        TimeSpan duration = cache.GetCacheDuration("/some/random/path");
        Assert.Equal(TimeSpan.FromMinutes(15), duration);
    }

    [Fact]
    public void Cache_GenerateCacheKey_FiltersSensitiveParameters()
    {
        TidalResponseCache cache = new();
        Dictionary<string, string> withoutSensitive = new() { ["countryCode"] = "US" };
        Dictionary<string, string> withSensitive = new()
        {
            ["countryCode"] = "US",
            ["sessionId"] = "secret-session-1",
            ["accessToken"] = "secret-token-1",
            ["refreshToken"] = "secret-refresh-1",
        };
        string keyA = cache.GenerateCacheKey("/albums/1", withoutSensitive);
        string keyB = cache.GenerateCacheKey("/albums/1", withSensitive);
        // sensitive parameters must be filtered, so keys should match
        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void Cache_Constructor_WithLogger_DoesNotThrow()
    {
        ILogger logger = NullLogger.Instance;
        TidalResponseCache cache = new(logger);
        Assert.NotNull(cache);
    }

}
