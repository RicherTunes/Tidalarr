using Lidarr.Plugin.Common.Services.Download;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// The single per-credential-set runtime bundle shared by <see cref="TidalLidarrIndexer"/>,
/// <see cref="TidalLidarrDownloadClient"/>, and <see cref="TidalFavoritesImportList"/> (D-12).
/// Wraps ONE <see cref="IServiceProvider"/> built over one ConfigPath so all three host classes
/// resolve the SAME singleton <c>TidalOAuthService</c> — refresh single-flight then actually
/// covers every consumer of the rotating-refresh-token file instead of three independent
/// services racing over it (the T-2 clobber class).
///
/// The download orchestrator is lazy: the indexer and import list never touch it, so its
/// HttpClient is only created once the download client first resolves a runtime.
///
/// Implements <see cref="IAsyncDisposable"/> so it can participate in the
/// <c>HostBridgeRuntimeCache</c> graveyard lifecycle (deferred provider disposal keeps
/// in-flight callers holding captured locals safe from <see cref="ObjectDisposedException"/>).
/// </summary>
public sealed class TidalRuntime : IAsyncDisposable
{
    private readonly Lazy<SimpleDownloadOrchestrator> _orchestrator;

    public IServiceProvider ServiceProvider { get; }

    /// <summary>Created on first access (download-client path only).</summary>
    public SimpleDownloadOrchestrator Orchestrator => _orchestrator.Value;

    // The three settings singletons registered in ServiceProvider. Held here so the per-side
    // Apply* methods can update them IN PLACE: the provider is keyed on ConfigPath only, so
    // side-specific fields (RedirectUrl, market, quality, concurrency, lyrics toggles) must
    // stay current without a rebuild. Consumers resolve these singleton instances and read
    // properties at use time, so in-place writes propagate.
    internal TidalarrSettings Aggregate { get; }
    internal TidalIndexerSettings IndexerSettings { get; }
    internal TidalDownloadClientSettings DownloadSettings { get; }

    internal TidalRuntime(
        IServiceProvider serviceProvider,
        TidalarrSettings aggregate,
        TidalIndexerSettings indexerSettings,
        TidalDownloadClientSettings downloadSettings)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        IndexerSettings = indexerSettings ?? throw new ArgumentNullException(nameof(indexerSettings));
        DownloadSettings = downloadSettings ?? throw new ArgumentNullException(nameof(downloadSettings));
        _orchestrator = new Lazy<SimpleDownloadOrchestrator>(
            () => TidalModule.CreateOrchestrator(ServiceProvider),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Refresh indexer-owned fields. RedirectUrl is the critical sub-state: the runtime is no
    /// longer rebuilt when the user pastes a fresh redirect URL (the cache key is ConfigPath
    /// only), so <c>ManagedTokenProvider.GetCredentials</c> — which reads
    /// <c>TidalarrSettings.RedirectUrl</c> at call time — must see the latest paste here.
    /// </summary>
    internal void ApplyIndexerSettings(TidalLidarrIndexerSettings settings)
    {
        Aggregate.RedirectUrl = settings.RedirectUrl;
        Aggregate.TidalMarket = settings.TidalMarket;
        Aggregate.EarlyReleaseLimit = settings.EarlyReleaseLimit;
        Aggregate.EnableCache = settings.EnableCache;
        Aggregate.CacheDuration = settings.CacheDuration;

        IndexerSettings.RedirectUrl = settings.RedirectUrl;
        IndexerSettings.TidalMarket = settings.TidalMarket;
        IndexerSettings.EarlyReleaseLimit = settings.EarlyReleaseLimit;
        IndexerSettings.EnableCache = settings.EnableCache;
        IndexerSettings.CacheDuration = settings.CacheDuration;
    }

    /// <summary>Refresh download-client-owned fields (quality, paths, concurrency, lyrics).</summary>
    internal void ApplyDownloadClientSettings(TidalLidarrDownloadClientSettings settings)
    {
        Aggregate.DownloadPath = settings.DownloadPath;
        Aggregate.PreferredQuality = settings.PreferredQuality;
        Aggregate.ExtractFlac = settings.ExtractFlac;
        Aggregate.DownloadDelay = settings.DownloadDelay;
        Aggregate.MaxConcurrentTrackDownloads = settings.MaxConcurrentTrackDownloads;
        Aggregate.MaxConcurrentChunkDownloads = settings.MaxConcurrentChunkDownloads;
        Aggregate.SaveSyncedLyrics = settings.SaveSyncedLyrics;
        Aggregate.UseLRCLIB = settings.UseLRCLIB;

        DownloadSettings.DownloadPath = settings.DownloadPath;
        DownloadSettings.PreferredQuality = settings.PreferredQuality;
        DownloadSettings.ExtractFlac = settings.ExtractFlac;
        DownloadSettings.DownloadDelay = settings.DownloadDelay;
        DownloadSettings.MaxConcurrentTrackDownloads = settings.MaxConcurrentTrackDownloads;
        DownloadSettings.MaxConcurrentChunkDownloads = settings.MaxConcurrentChunkDownloads;
        DownloadSettings.SaveSyncedLyrics = settings.SaveSyncedLyrics;
        DownloadSettings.UseLRCLIB = settings.UseLRCLIB;
    }

    /// <summary>
    /// Refresh import-list-owned fields. Deliberately does NOT touch RedirectUrl: the import
    /// list has no redirect input, and clobbering the aggregate with an empty string would
    /// erase the indexer's live credential sub-state on every scheduled favorites fetch.
    /// </summary>
    internal void ApplyImportListSettings(TidalFavoritesImportListSettings settings)
    {
        Aggregate.TidalMarket = settings.TidalMarket;
        IndexerSettings.TidalMarket = settings.TidalMarket;
    }

    public async ValueTask DisposeAsync()
    {
        // Disposing the provider tears down every registered HttpClient/handler chain —
        // same semantics the three pre-D-12 runtime wrappers had. The orchestrator holds a
        // factory-pooled HttpClient whose handler lifetime the provider owns, so no separate
        // orchestrator disposal is needed (SimpleDownloadOrchestrator is not IDisposable).
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
