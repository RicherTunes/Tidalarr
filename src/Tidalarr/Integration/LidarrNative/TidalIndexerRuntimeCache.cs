using System.Security.Cryptography;
using System.Text;
using Lidarr.Plugin.Common.HostBridge;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Process-wide runtime cache for <see cref="TidalLidarrIndexer"/>.
///
/// Adopts <see cref="HostBridgeRuntimeCache{TRuntime,TSettings}"/> (Common Wave D item 6),
/// replacing the per-instance double-checked-lock pattern. Key gain: when
/// <see cref="TidalLidarrIndexerSettings.ConfigPath"/> or
/// <see cref="TidalLidarrIndexerSettings.RedirectUrl"/> changes between calls (credential
/// rotation), the stale <see cref="IServiceProvider"/> is parked in the graveyard for 60 s
/// and a fresh one is built — no manual invalidation required.
///
/// Static singleton so Lidarr's multi-instance reflection construction shares one runtime.
/// </summary>
internal sealed class TidalIndexerRuntimeCache
    : HostBridgeRuntimeCache<TidalIndexerRuntime, TidalLidarrIndexerSettings>
{
    public static readonly TidalIndexerRuntimeCache Shared = new();

    private TidalIndexerRuntimeCache() { }

    /// <inheritdoc/>
    protected override string ComputeAuthKey(TidalLidarrIndexerSettings settings)
    {
        // Hash only credential-critical fields. Non-critical fields (market, cache TTL, etc.)
        // do NOT trigger a rebuild — changing them reuses the existing runtime.
        string raw = $"{settings.ConfigPath}|{settings.RedirectUrl}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    /// <inheritdoc/>
    protected override Task<TidalIndexerRuntime?> CreateAsync(
        TidalLidarrIndexerSettings settings,
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
            RedirectUrl = settings.RedirectUrl,
            TidalMarket = settings.TidalMarket,
            EarlyReleaseLimit = settings.EarlyReleaseLimit,
            EnableCache = settings.EnableCache,
            CacheDuration = settings.CacheDuration
        };
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(new TidalarrSettings
        {
            ConfigPath = settings.ConfigPath,
            RedirectUrl = settings.RedirectUrl,
            TidalMarket = settings.TidalMarket
        });

        TidalModule.RegisterServices(services);

        IServiceProvider sp = services.BuildServiceProvider();
        return Task.FromResult<TidalIndexerRuntime?>(new TidalIndexerRuntime(sp));
    }
}
