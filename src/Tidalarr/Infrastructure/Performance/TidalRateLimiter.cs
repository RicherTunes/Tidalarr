using Lidarr.Plugin.Common.Services.Performance;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Infrastructure.Performance;

/// <summary>
/// Tidal-specific adapter around the shared UniversalAdaptiveRateLimiter.
/// Ensures all limiter operations are consistently tagged with the "Tidal" service name
/// and exposes convenience helpers for diagnostics.
/// </summary>
public sealed class TidalRateLimiter : NamedServiceRateLimiter
{
    private readonly ILogger<TidalRateLimiter>? _logger;

    public TidalRateLimiter(ILogger<TidalRateLimiter>? logger = null)
        : base("Tidal")
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task<bool> WaitIfNeededAsync(string service, string endpoint, CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("Rate limiter wait request for {Service}:{Endpoint}", service, endpoint);
        return base.WaitIfNeededAsync(service, endpoint, cancellationToken);
    }

    /// <summary>Returns rate-limit stats for the Tidal service.</summary>
    public ServiceRateLimitStats GetTidalStats() => GetNamedServiceStats();
}
