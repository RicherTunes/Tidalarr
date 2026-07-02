using Microsoft.Extensions.Logging;
using Tidalarr.Infrastructure.Caching;

namespace Tidalarr.Integration;

/// <summary>
/// Builds a <see cref="TidalResponseCache"/> configured from the indexer's "Enable Cache" /
/// "Cache Duration" settings.
///
/// T-3 (external dead-settings audit): both settings were accepted, validated, and copied between
/// settings DTOs across the plugin (TidalModule.cs, the runtime caches, TidalarrPlugin.cs's schema
/// shim), but the DI-constructed <see cref="TidalResponseCache"/> was always built with its
/// parameterless constructor — the copied values were never actually read anywhere. This factory is
/// the one place that translates the settings into real cache behavior.
/// </summary>
internal static class TidalResponseCacheFactory
{
    public static TidalResponseCache Create(TidalIndexerSettings? settings, ILogger? logger = null)
    {
        bool enableCache = settings?.EnableCache ?? true;
        int cacheDurationMinutes = settings?.CacheDuration ?? 15;

        return new TidalResponseCache(
            logger: logger,
            enableCache: enableCache,
            searchCacheDuration: TimeSpan.FromMinutes(Math.Max(0, cacheDurationMinutes)));
    }
}
