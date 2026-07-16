using System.Security.Cryptography;
using System.Text;
using Lidarr.Plugin.Common.HostBridge;
using Microsoft.Extensions.DependencyInjection;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// The single process-wide runtime cache for ALL Tidal host classes (D-12, follow-up to the
/// T-2 refresh-race fix). Replaces the three per-host-class caches (indexer / download client /
/// import list) that each built their OWN <see cref="IServiceProvider"/> — and therefore their
/// own singleton <c>TidalOAuthService</c> — over the same rotating-refresh-token file.
/// One credential set now maps to ONE provider, so refresh single-flight genuinely covers
/// every consumer instead of three independent services racing over <c>tidal_tokens.json</c>.
///
/// <para><b>Key scheme: ConfigPath ONLY.</b> The credential identity of a runtime is the token
/// file, and the token file lives at <c>{ConfigPath}/tidal_tokens.json</c>. The old indexer
/// cache additionally keyed on RedirectUrl, but RedirectUrl is a one-time OAuth INPUT, not
/// credential state:</para>
/// <list type="bullet">
///   <item>The code exchange (<c>TidalLidarrIndexer.TryExchangeAuthorizationCode</c>) reads the
///         freshly-pasted URL from the HOST settings object per call — never from the
///         provider-registered snapshot.</item>
///   <item>The only provider-side reader is <c>ManagedTokenProvider.GetCredentials</c>, whose
///         <c>TidalCredentials</c> value is ignored by <c>TidalAuthTokenAuthAdapter</c>
///         (it delegates to <c>ITidalAuth.GetValidTokensAsync()</c>, i.e. the persisted store).</item>
/// </list>
/// <para>So two RedirectUrls over one ConfigPath SHOULD share a provider: they authenticate the
/// same token file, and sharing keeps freshly-exchanged tokens visible to searches without a
/// rebuild. The paste is still propagated as per-side sub-state
/// (<see cref="TidalRuntime.ApplyIndexerSettings"/> updates the registered settings in place),
/// and the indexer clears the token manager's in-memory session after a successful exchange —
/// the two effects the old rebuild-on-RedirectUrl-change delivered incidentally.
/// Pinned by <c>TidalRuntimeCacheTests.RedirectUrlChange_SameConfigPath_ReusesProvider_AndUpdatesSubState</c>.</para>
///
/// <para>Auth-key invalidation, single-flight construction, and the 60s deferred-disposal
/// graveyard all come from <see cref="HostBridgeRuntimeCache{TRuntime,TSettings}"/>.</para>
/// </summary>
internal sealed class TidalRuntimeCache : HostBridgeRuntimeCache<TidalRuntime, TidalRuntimeCache.CredentialIdentity>
{
    /// <summary>Process-wide instance shared by all host classes (Lidarr constructs them via reflection).</summary>
    public static readonly TidalRuntimeCache Shared = new();

    // Internal (not private): tests construct isolated instances so they never leak state
    // through Shared. Production code must use Shared.
    internal TidalRuntimeCache() { }

    /// <summary>The credential identity of a runtime: the ConfigPath that owns the token file.</summary>
    internal sealed record CredentialIdentity(string ConfigPath);

    /// <summary>Resolve the shared runtime for the indexer, refreshing indexer-owned sub-state.</summary>
    public async Task<TidalRuntime?> GetForIndexerAsync(TidalLidarrIndexerSettings settings, CancellationToken cancellationToken = default)
    {
        TidalRuntime? runtime = await GetAsync(new CredentialIdentity(settings.ConfigPath ?? string.Empty), cancellationToken).ConfigureAwait(false);
        runtime?.ApplyIndexerSettings(settings);
        return runtime;
    }

    /// <summary>Resolve the shared runtime for the download client, refreshing download-owned sub-state.</summary>
    public async Task<TidalRuntime?> GetForDownloadClientAsync(TidalLidarrDownloadClientSettings settings, CancellationToken cancellationToken = default)
    {
        TidalRuntime? runtime = await GetAsync(new CredentialIdentity(settings.ConfigPath ?? string.Empty), cancellationToken).ConfigureAwait(false);
        runtime?.ApplyDownloadClientSettings(settings);
        return runtime;
    }

    /// <summary>Resolve the shared runtime for the favorites import list, refreshing list-owned sub-state.</summary>
    public async Task<TidalRuntime?> GetForImportListAsync(TidalFavoritesImportListSettings settings, CancellationToken cancellationToken = default)
    {
        TidalRuntime? runtime = await GetAsync(new CredentialIdentity(settings.ConfigPath ?? string.Empty), cancellationToken).ConfigureAwait(false);
        runtime?.ApplyImportListSettings(settings);
        return runtime;
    }

    /// <inheritdoc/>
    protected override string ComputeAuthKey(CredentialIdentity identity)
    {
        // ConfigPath only — see the class doc for why RedirectUrl is deliberately excluded.
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ConfigPath));
        return Convert.ToHexString(hash);
    }

    /// <inheritdoc/>
    protected override Task<TidalRuntime?> CreateAsync(CredentialIdentity identity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identity.ConfigPath))
        {
            return Task.FromResult<TidalRuntime?>(null);
        }

        ServiceCollection services = new();

        // Register the three settings singletons as INSTANCES (not factories) so the runtime
        // can update them in place when a host class arrives with fresh side-specific fields
        // (see TidalRuntime.Apply*). Instance registration also pre-empts TidalModule's
        // TryAddSingleton back-compat mappings.
        TidalarrSettings aggregate = new()
        {
            ConfigPath = identity.ConfigPath,
            RedirectUrl = string.Empty
        };
        TidalIndexerSettings indexerSettings = new()
        {
            ConfigPath = identity.ConfigPath,
            RedirectUrl = string.Empty
        };
        TidalDownloadClientSettings downloadSettings = new();

        _ = services.AddSingleton(aggregate);
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(downloadSettings);

        TidalModule.RegisterServices(services);

        IServiceProvider sp = services.BuildServiceProvider();
        return Task.FromResult<TidalRuntime?>(new TidalRuntime(sp, aggregate, indexerSettings, downloadSettings));
    }
}
