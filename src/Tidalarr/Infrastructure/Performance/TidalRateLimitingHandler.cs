using Lidarr.Plugin.Common.Services.Performance;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Tidalarr.Infrastructure.Performance;

/// <summary>
/// HttpClient delegating handler that gates every Tidal egress through
/// <see cref="TidalRateLimiter"/> (wraps common's <c>UniversalAdaptiveRateLimiter</c>).
///
/// Background: prior to this handler, <see cref="TidalRateLimiter"/> was registered in DI
/// but never invoked from any production path — Tidal API calls (search, GetAlbum, GetTrack,
/// GetStreamInfo, GetPlaybackInfo) and chunk downloads (TidalChunkDownloader) all hit
/// <c>api.tidal.com</c> through naked <see cref="HttpClient"/>. Lidarr fans out searches per
/// artist/album in parallel and a single album refresh can trigger 60-250+ HTTP calls
/// (chunk fetches dominate). Without a single global gate, default-settings users hit
/// HTTP 429 routinely.
///
/// This handler converts the previously-dead limiter into a hard ceiling that every
/// HttpClient registered via <c>services.AddHttpClient&lt;T&gt;().AddHttpMessageHandler&lt;TidalRateLimitingHandler&gt;()</c>
/// must pass through. The endpoint key is derived from the request URI's host + path
/// segment so different Tidal endpoints (api.tidal.com vs *.audio.tidal.com chunk hosts)
/// get independently-tracked budgets.
///
/// 429 responses with a <c>Retry-After</c> header trigger a respectful pause before
/// returning to the caller — Polly's retry above this layer (or the limiter's internal
/// adaptive backoff) then handles the actual retry.
/// </summary>
public sealed class TidalRateLimitingHandler : DelegatingHandler
{
    private const string Service = "Tidal";
    private readonly IUniversalAdaptiveRateLimiter _rateLimiter;
    private readonly ILogger<TidalRateLimitingHandler>? _logger;

    public TidalRateLimitingHandler(IUniversalAdaptiveRateLimiter rateLimiter, ILogger<TidalRateLimitingHandler>? logger = null)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string endpointKey = BuildEndpointKey(request);

        // Pre-send: wait for the limiter to permit this request. The limiter can
        // delay (token bucket) or short-circuit when an upstream 429 cooldown is
        // active. Returns false only when the limiter is disposed; treat as best-effort
        // and continue (don't block the entire pipeline on a disposal race).
        try
        {
            await _rateLimiter.WaitIfNeededAsync(Service, endpointKey, cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Limiter shut down (plugin disposing). Continue without gating.
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Post-send: feed the response back to the limiter for adaptive adjustment
        // (slows on 429/5xx, speeds back up on sustained 2xx). RecordResponse is a
        // no-op when the limiter is disposed.
        try
        {
            _rateLimiter.RecordResponse(Service, endpointKey, response);
        }
        catch (ObjectDisposedException) { /* limiter shut down mid-request */ }

        // If we got a 429 with a Retry-After header, honor it BEFORE returning so
        // the caller's retry policy waits the right amount of time. Without this
        // honor-the-header step the caller's exponential backoff will likely retry
        // too fast and eat through the bucket again.
        if (response.StatusCode == HttpStatusCode.TooManyRequests && response.Headers.RetryAfter is { } retryAfter)
        {
            TimeSpan delay = ResolveRetryAfter(retryAfter);
            if (delay > TimeSpan.Zero)
            {
                _logger?.LogWarning("Tidal returned 429 for {Endpoint}; honoring Retry-After of {Delay}", endpointKey, delay);
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Caller cancelled; fall through and return the 429 response as-is.
                }
            }
        }

        return response;
    }

    /// <summary>
    /// Build a stable endpoint key from the request URI. The limiter tracks budgets per
    /// (service, endpoint) tuple, so distinct paths get independent histories. We use
    /// host + first path segment to balance specificity vs. cardinality:
    /// - <c>api.tidal.com/v1/search</c> → <c>api.tidal.com:v1</c>
    /// - <c>sp-ap-eu.audio.tidal.com/.../seg.mp4</c> → <c>sp-ap-eu.audio.tidal.com:</c>
    /// </summary>
    private static string BuildEndpointKey(HttpRequestMessage request)
    {
        Uri? uri = request.RequestUri;
        if (uri is null) return "unknown";
        string host = uri.Host;
        string firstSeg = uri.Segments.Length > 1 ? uri.Segments[1].TrimEnd('/') : string.Empty;
        return $"{host}:{firstSeg}";
    }

    private static TimeSpan ResolveRetryAfter(System.Net.Http.Headers.RetryConditionHeaderValue retryAfter)
    {
        if (retryAfter.Delta is { } delta) return delta;
        if (retryAfter.Date is { } date)
        {
            TimeSpan untilDate = date - DateTimeOffset.UtcNow;
            return untilDate > TimeSpan.Zero ? untilDate : TimeSpan.Zero;
        }
        return TimeSpan.Zero;
    }
}
