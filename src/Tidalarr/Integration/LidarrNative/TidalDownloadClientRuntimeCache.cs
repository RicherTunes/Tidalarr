using System.Security.Cryptography;
using System.Text;
using Lidarr.Plugin.Common.HostBridge;
using Lidarr.Plugin.Common.Services.Download;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Process-wide runtime cache for <see cref="TidalLidarrDownloadClient"/>.
///
/// Adopts <see cref="HostBridgeRuntimeCache{TRuntime,TSettings}"/> (Common Wave D item 6).
/// Auth key is derived from <see cref="TidalLidarrDownloadClientSettings.ConfigPath"/> only
/// (tokens are stored there and shared with the indexer — no separate DC credentials).
/// On a ConfigPath change, the stale <see cref="IServiceProvider"/> is parked in the
/// graveyard so in-flight downloads using captured locals don't crash with
/// <see cref="ObjectDisposedException"/>.
///
/// Static singleton so Lidarr's multi-instance reflection construction shares one runtime.
/// </summary>
internal sealed class TidalDownloadClientRuntimeCache
    : HostBridgeRuntimeCache<TidalDownloadClientRuntime, TidalLidarrDownloadClientSettings>
{
    public static readonly TidalDownloadClientRuntimeCache Shared = new();

    private TidalDownloadClientRuntimeCache() { }

    /// <inheritdoc/>
    protected override string ComputeAuthKey(TidalLidarrDownloadClientSettings settings)
    {
        // ConfigPath is the only credential-critical field for the DC: it determines where
        // the shared OAuth tokens (written by the indexer) are read from.
        // Quality/path/concurrency changes reuse the existing runtime.
        string raw = settings.ConfigPath ?? string.Empty;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    /// <inheritdoc/>
    protected override Task<TidalDownloadClientRuntime?> CreateAsync(
        TidalLidarrDownloadClientSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ConfigPath))
        {
            return Task.FromResult<TidalDownloadClientRuntime?>(null);
        }

        ServiceCollection services = new();

        TidalDownloadClientSettings downloadSettings = settings.ToTidalSettings();
        _ = services.AddSingleton(downloadSettings);

        TidalarrSettings tidalarrSettings = new()
        {
            ConfigPath = settings.ConfigPath,
            RedirectUrl = string.Empty,
            DownloadPath = settings.DownloadPath,
            PreferredQuality = settings.PreferredQuality,
            ExtractFlac = settings.ExtractFlac,
            DownloadDelay = settings.DownloadDelay,
            MaxConcurrentTrackDownloads = settings.MaxConcurrentTrackDownloads,
            MaxConcurrentChunkDownloads = settings.MaxConcurrentChunkDownloads
        };
        _ = services.AddSingleton(tidalarrSettings);

        TidalModule.RegisterServices(services);

        IServiceProvider sp = services.BuildServiceProvider();
        SimpleDownloadOrchestrator orchestrator = TidalModule.CreateOrchestrator(sp);
        return Task.FromResult<TidalDownloadClientRuntime?>(new TidalDownloadClientRuntime(sp, orchestrator));
    }
}
