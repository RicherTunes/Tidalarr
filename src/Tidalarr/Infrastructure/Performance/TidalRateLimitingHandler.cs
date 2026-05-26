using Lidarr.Plugin.Common.Services.Http;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Infrastructure.Performance;

/// <summary>
/// Thin Tidal-specific subclass of <see cref="AdaptiveRateLimitingHandler"/>.
/// All logic lives in Common; this class exists only to satisfy DI registration
/// (AddHttpMessageHandler&lt;TidalRateLimitingHandler&gt;) and to bind the
/// typed logger.
///
/// <para>
/// Migration note: the full 95-LOC implementation was lifted to
/// <c>Lidarr.Plugin.Common.Services.Http.AdaptiveRateLimitingHandler</c> (wave-23).
/// </para>
/// </summary>
public sealed class TidalRateLimitingHandler : AdaptiveRateLimitingHandler
{
    public TidalRateLimitingHandler(
        TidalRateLimiter rateLimiter,
        ILogger<TidalRateLimitingHandler>? logger = null)
        : base(rateLimiter, "Tidal", logger)
    {
    }
}
