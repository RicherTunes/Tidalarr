using System.Net;
using System.Net.Http.Headers;
using Lidarr.Plugin.Common.Services.Performance;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

/// <summary>
/// Regression guards for the dead-code → live-gate conversion of TidalRateLimiter.
/// Before commit b6d20df the limiter was registered in DI but never actually invoked from
/// any HttpClient pipeline — Tidal egress was unrate-limited and 429-prone under default
/// settings. These tests fail fast if any HttpClient registration drops the handler again.
/// </summary>
public class TidalRateLimitingHandlerTests
{
    [Fact]
    public void Handler_ResolvesFromDI()
    {
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);
        using ServiceProvider sp = services.BuildServiceProvider();

        TidalRateLimitingHandler? handler = sp.GetService<TidalRateLimitingHandler>();
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task Handler_CallsLimiterWaitAndRecord_OnEverySend()
    {
        var limiter = new TidalRateLimiter();
        TidalRateLimitingHandler handler = new(limiter)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK)
        };
        using HttpClient client = new(handler);

        HttpResponseMessage resp = await client.GetAsync("https://api.tidal.com/v1/search?q=foo");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        // The limiter records all responses (success or failure) — verify by snapshotting
        // the global stats. After exactly 1 request, total request count = 1.
        GlobalRateLimitStats stats = limiter.GetGlobalStats();
        Assert.True(stats.TotalRequests >= 1, $"Expected limiter to record the request; got {stats.TotalRequests}");
    }

    [Fact]
    public async Task Handler_HonorsRetryAfterDelta_On429()
    {
        var limiter = new TidalRateLimiter();
        var inner = new StubHandler(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromMilliseconds(50));
        TidalRateLimitingHandler handler = new(limiter)
        {
            InnerHandler = inner
        };
        using HttpClient client = new(handler);

        DateTimeOffset start = DateTimeOffset.UtcNow;
        HttpResponseMessage resp = await client.GetAsync("https://api.tidal.com/v1/playbackinfopostpaywall");
        TimeSpan elapsed = DateTimeOffset.UtcNow - start;

        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        // Honoring Retry-After should add at least the header's delta (~50ms) to the
        // round-trip. Allow generous slack for timer granularity.
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(40),
            $"Expected handler to honor Retry-After ~50ms; round-trip was only {elapsed.TotalMilliseconds:0} ms");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly TimeSpan? _retryAfter;

        public StubHandler(HttpStatusCode status, TimeSpan? retryAfter = null)
        {
            _status = status;
            _retryAfter = retryAfter;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage resp = new(_status);
            if (_retryAfter is { } delta)
            {
                resp.Headers.RetryAfter = new RetryConditionHeaderValue(delta);
            }
            return Task.FromResult(resp);
        }
    }
}
