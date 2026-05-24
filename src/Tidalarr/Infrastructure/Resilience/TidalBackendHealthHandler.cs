using System.Net;
using Lidarr.Plugin.Common.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tidalarr.Infrastructure.Resilience;

/// <summary>
/// Delegating handler that gates outbound Tidal HTTP requests through
/// <see cref="BackendHealthCache"/>. When the target host suffered a
/// connection-class failure (SocketException / DNS) within the last
/// <see cref="BackendHealthCache.DefaultGraceSeconds"/> seconds, the call
/// short-circuits immediately with an <see cref="HttpRequestException"/> rather
/// than burning the full retry budget.
///
/// <para>
/// Provider keys follow the per-host convention:
/// <list type="bullet">
/// <item><description><c>"tidal:auth"</c> — auth.tidal.com</description></item>
/// <item><description><c>"tidal:api"</c> — api.tidal.com / api.tidalhifi.com</description></item>
/// <item><description><c>"tidal:cdn"</c> — any other Tidal host (chunk CDN, etc.)</description></item>
/// </list>
/// </para>
///
/// <para>
/// This gate is independent of <c>AuthFailureGate</c>. Auth-gate trips on
/// repeated 401/403 (auth-failure cascade); this gate trips on repeated
/// connection-refused / DNS failures (network-down cascade). Both coexist.
/// </para>
/// </summary>
public sealed class TidalBackendHealthHandler : DelegatingHandler
{
    private readonly BackendHealthCache _cache;
    private readonly ILogger _logger;

    /// <summary>Production constructor — uses the process-wide singleton cache.</summary>
    public TidalBackendHealthHandler(ILogger<TidalBackendHealthHandler>? logger = null)
        : this(BackendHealthCache.Shared, (ILogger?)logger) { }

    /// <summary>Test constructor — accepts an isolated <see cref="BackendHealthCache"/> instance.</summary>
    internal TidalBackendHealthHandler(BackendHealthCache cache, ILogger? logger = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? NullLogger.Instance;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string host = request.RequestUri?.Host ?? string.Empty;
        string provider = ClassifyProvider(host);
        string baseUrl = ExtractBaseUrl(request.RequestUri);

        // Fast-fail: check cache BEFORE sending the request.
        if (_cache.IsKnownDown(provider, baseUrl, out string? downReason))
        {
            _logger.LogDebug("[BackendHealthCache] Skipping Tidal request to {BaseUrl} — {DownReason}", baseUrl, downReason);
            throw new HttpRequestException($"Tidal backend known-down: {downReason}",
                inner: null,
                statusCode: null);
        }

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // On any successful HTTP exchange clear the down-state so future calls go through.
            if (response.IsSuccessStatusCode)
            {
                _cache.MarkUp(provider, baseUrl);
            }

            return response;
        }
        catch (HttpRequestException ex) when (BackendHealthCache.IsConnectionClassFailure(ex))
        {
            _cache.MarkDown(provider, baseUrl, ex);
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout / cancellation — not a connection-class failure; don't mark down.
            throw;
        }
    }

    /// <summary>
    /// Maps a Tidal host to a stable provider key.
    /// </summary>
    private static string ClassifyProvider(string host)
    {
        return host.Equals("auth.tidal.com", StringComparison.OrdinalIgnoreCase)
            ? "tidal:auth"
            : (host.Equals("api.tidal.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("api.tidalhifi.com", StringComparison.OrdinalIgnoreCase))
                ? "tidal:api"
                : "tidal:cdn";
    }

    /// <summary>
    /// Returns the host-only base URL (scheme + host) so all paths on the same host
    /// share one cache slot, as prescribed by BackendHealthCache docs.
    /// </summary>
    private static string ExtractBaseUrl(Uri? uri)
    {
        if (uri is null) return string.Empty;
        return uri.IsAbsoluteUri
            ? $"{uri.Scheme}://{uri.Host}"
            : uri.Host;
    }
}
