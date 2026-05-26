using Lidarr.Plugin.Common.Resilience;
using Lidarr.Plugin.Common.Services.Http;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Infrastructure.Resilience;

/// <summary>
/// Thin Tidal-specific subclass of <see cref="BackendHealthDelegatingHandler"/>.
/// All logic lives in Common; this class exists only to satisfy DI registration
/// and to supply the Tidal-specific host→provider classification.
///
/// <para>Provider key mapping:</para>
/// <list type="bullet">
/// <item><description><c>"tidal:auth"</c> — auth.tidal.com</description></item>
/// <item><description><c>"tidal:api"</c>  — api.tidal.com / api.tidalhifi.com</description></item>
/// <item><description><c>"tidal:cdn"</c>  — any other Tidal host (chunk CDN, etc.)</description></item>
/// </list>
///
/// <para>
/// Migration note: the full 112-LOC implementation was lifted to
/// <c>Lidarr.Plugin.Common.Services.Http.BackendHealthDelegatingHandler</c> (wave-23).
/// </para>
/// </summary>
public sealed class TidalBackendHealthHandler : BackendHealthDelegatingHandler
{
    /// <summary>Production constructor — uses the process-wide singleton cache.</summary>
    public TidalBackendHealthHandler(ILogger<TidalBackendHealthHandler>? logger = null)
        : base(BackendHealthCache.Shared, ClassifyProvider, logger) { }

    /// <summary>Test constructor — accepts an isolated <see cref="BackendHealthCache"/> instance.</summary>
    internal TidalBackendHealthHandler(BackendHealthCache cache, ILogger? logger = null)
        : base(cache, ClassifyProvider, logger) { }

    /// <summary>Maps a Tidal host to a stable provider key.</summary>
    private static string ClassifyProvider(string host)
    {
        return host.Equals("auth.tidal.com", StringComparison.OrdinalIgnoreCase)
            ? "tidal:auth"
            : (host.Equals("api.tidal.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("api.tidalhifi.com", StringComparison.OrdinalIgnoreCase))
                ? "tidal:api"
                : "tidal:cdn";
    }
}
