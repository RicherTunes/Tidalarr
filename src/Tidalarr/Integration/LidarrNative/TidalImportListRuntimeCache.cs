using System.Security.Cryptography;
using System.Text;
using Lidarr.Plugin.Common.HostBridge;
using Microsoft.Extensions.DependencyInjection;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Process-wide runtime cache for <see cref="TidalFavoritesImportList"/>.
///
/// Mirrors <see cref="TidalIndexerRuntimeCache"/>: adopts
/// <see cref="HostBridgeRuntimeCache{TRuntime,TSettings}"/> so a change to
/// <see cref="TidalFavoritesImportListSettings.ConfigPath"/> (credential rotation) parks the stale
/// <see cref="IServiceProvider"/> in the graveyard and builds a fresh one, with no manual
/// invalidation. The built provider exposes <c>ITidalCore</c> + <c>ITidalAuth</c> reading the same
/// token store the indexer authenticated, so the import list needs no separate login.
///
/// Reuses <see cref="TidalIndexerRuntime"/> (a thin <see cref="IServiceProvider"/> wrapper).
/// </summary>
internal sealed class TidalImportListRuntimeCache
    : HostBridgeRuntimeCache<TidalIndexerRuntime, TidalFavoritesImportListSettings>
{
    public static readonly TidalImportListRuntimeCache Shared = new();

    private TidalImportListRuntimeCache() { }

    /// <inheritdoc/>
    protected override string ComputeAuthKey(TidalFavoritesImportListSettings settings)
    {
        // Only ConfigPath is credential-critical for the import list (auth is via the shared token
        // store). Market/Content changes reuse the existing runtime.
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(settings.ConfigPath ?? string.Empty));
        return Convert.ToHexString(hash);
    }

    /// <inheritdoc/>
    protected override Task<TidalIndexerRuntime?> CreateAsync(
        TidalFavoritesImportListSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ConfigPath))
        {
            return Task.FromResult<TidalIndexerRuntime?>(null);
        }

        ServiceCollection services = new();

        TidalIndexerSettings indexerSettings = new()
        {
            ConfigPath = settings.ConfigPath,
            RedirectUrl = string.Empty,
            TidalMarket = settings.TidalMarket
        };
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(new TidalarrSettings
        {
            ConfigPath = settings.ConfigPath,
            RedirectUrl = string.Empty,
            TidalMarket = settings.TidalMarket
        });

        TidalModule.RegisterServices(services);

        IServiceProvider sp = services.BuildServiceProvider();
        return Task.FromResult<TidalIndexerRuntime?>(new TidalIndexerRuntime(sp));
    }
}
