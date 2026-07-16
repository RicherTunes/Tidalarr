using System.Security.Cryptography;
using System.Text;
using Lidarr.Plugin.Common.HostBridge;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Interfaces;
using Tidalarr.Integration;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Integration;

/// <summary>
/// Unit tests for the HostBridgeRuntimeCache adoption in Tidalarr (Wave D item 6).
///
/// Uses a thin <see cref="StubRuntimeCache"/> so tests never touch TidalModule or
/// Lidarr host assemblies — pure cache semantics only.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Area", "RuntimeCache")]
public class TidalRuntimeCacheTests
{
    // ---------------------------------------------------------------------------
    // Minimal stub types
    // ---------------------------------------------------------------------------

    private sealed class StubRuntime : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubSettings
    {
        public string Token { get; init; } = string.Empty;
        public string BaseUrl { get; init; } = "https://api.example.com";
    }

    private sealed class StubCache : HostBridgeRuntimeCache<StubRuntime, StubSettings>
    {
        public int BuildCount { get; private set; }
        public bool ReturnNull { get; set; }

        // Expose GraveyardLingerSeconds so tests can bypass the 60-second default.
        protected override int GraveyardLingerSeconds => 0;

        protected override string ComputeAuthKey(StubSettings settings)
        {
            string raw = $"{settings.Token}|{settings.BaseUrl}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        protected override Task<StubRuntime?> CreateAsync(StubSettings settings, CancellationToken cancellationToken)
        {
            BuildCount++;
            if (ReturnNull)
            {
                return Task.FromResult<StubRuntime?>(null);
            }
            return Task.FromResult<StubRuntime?>(new StubRuntime());
        }
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetOrBuild_FirstCall_BuildsRuntime()
    {
        StubCache cache = new();
        StubSettings settings = new() { Token = "tok-1" };

        StubRuntime? result = await cache.GetAsync(settings);

        Assert.NotNull(result);
        Assert.Equal(1, cache.BuildCount);
    }

    [Fact]
    public async Task GetOrBuild_SameSettings_ReturnsCachedRuntime()
    {
        StubCache cache = new();
        StubSettings settings = new() { Token = "tok-same" };

        StubRuntime? first = await cache.GetAsync(settings);
        StubRuntime? second = await cache.GetAsync(settings);

        Assert.NotNull(first);
        Assert.Same(first, second); // identical reference — cache hit
        Assert.Equal(1, cache.BuildCount);
    }

    [Fact]
    public async Task GetOrBuild_SettingsChanged_RebuildsRuntime()
    {
        StubCache cache = new();
        StubSettings settingsV1 = new() { Token = "tok-v1" };
        StubSettings settingsV2 = new() { Token = "tok-v2" }; // different token → different auth key

        StubRuntime? first = await cache.GetAsync(settingsV1);
        StubRuntime? second = await cache.GetAsync(settingsV2);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second); // new runtime on credential change
        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public async Task GetOrBuild_CreateReturnsNull_PropagatesNull()
    {
        StubCache cache = new() { ReturnNull = true };
        StubSettings settings = new() { Token = "empty-creds" };

        StubRuntime? result = await cache.GetAsync(settings);

        Assert.Null(result);
        Assert.Equal(1, cache.BuildCount); // tried to build but got null — not cached
    }

    [Fact]
    public async Task Invalidate_ClearsCache_NextCallRebuilds()
    {
        StubCache cache = new();
        StubSettings settings = new() { Token = "tok-reset" };

        StubRuntime? first = await cache.GetAsync(settings);
        await cache.ResetAsync();
        StubRuntime? second = await cache.GetAsync(settings);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second); // post-reset build produces a new instance
        Assert.True(first.Disposed);   // ResetAsync disposed the evicted runtime
        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public async Task ConcurrentBuilds_OnlyOneFiresBuildFn()
    {
        // Use a cache with an artificial build delay to stress the gate.
        SlowBuildCache cache = new();
        StubSettings settings = new() { Token = "tok-concurrent" };

        // Launch many concurrent readers.
        Task<StubRuntime?>[] tasks = Enumerable.Range(0, 20)
            .Select(_ => cache.GetAsync(settings))
            .ToArray();

        StubRuntime?[] results = await Task.WhenAll(tasks);

        // All callers must see a non-null runtime.
        Assert.All(results, r => Assert.NotNull(r));

        // The build function must have fired exactly once (gate worked).
        Assert.Equal(1, cache.BuildCount);

        // All callers must see the same runtime instance.
        StubRuntime? reference = results[0];
        Assert.All(results, r => Assert.Same(reference, r));
    }

    /// <summary>Adds a brief artificial delay during CreateAsync to expose gate races.</summary>
    private sealed class SlowBuildCache : HostBridgeRuntimeCache<StubRuntime, StubSettings>
    {
        public int BuildCount { get; private set; }

        protected override string ComputeAuthKey(StubSettings settings)
        {
            string raw = $"{settings.Token}|{settings.BaseUrl}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        protected override async Task<StubRuntime?> CreateAsync(StubSettings settings, CancellationToken cancellationToken)
        {
            BuildCount++;
            await Task.Delay(30, cancellationToken); // simulate slow DI container build
            return new StubRuntime();
        }
    }

    // ---------------------------------------------------------------------------
    // Auth-key semantics: only the credential identity (ConfigPath — where the token
    // file lives) triggers a rebuild. Mirrors TidalRuntimeCache.ComputeAuthKey (D-12).
    // ---------------------------------------------------------------------------

    private sealed class IndexerAuthKeyCache : HostBridgeRuntimeCache<StubRuntime, TidalLidarrIndexerSettings>
    {
        public int BuildCount { get; private set; }

        protected override string ComputeAuthKey(TidalLidarrIndexerSettings settings)
        {
            // ConfigPath only — same scheme as the production TidalRuntimeCache (RedirectUrl
            // is a one-time OAuth input, not credential state; see that class's doc).
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(settings.ConfigPath ?? string.Empty));
            return Convert.ToHexString(hash);
        }

        protected override Task<StubRuntime?> CreateAsync(TidalLidarrIndexerSettings settings, CancellationToken cancellationToken)
        {
            BuildCount++;
            return Task.FromResult<StubRuntime?>(new StubRuntime());
        }
    }

    [Fact]
    public async Task IndexerAuthKey_NonCredentialFieldChange_DoesNotRebuild()
    {
        IndexerAuthKeyCache cache = new();
        TidalLidarrIndexerSettings s1 = new()
        {
            ConfigPath = "/tmp/tidal",
            RedirectUrl = "https://example.com/cb",
            TidalMarket = "US",
            CacheDuration = 15
        };
        TidalLidarrIndexerSettings s2 = new()
        {
            ConfigPath = "/tmp/tidal",      // same credential identity
            RedirectUrl = "https://example.com/cb2", // different — but NOT part of the key (D-12)
            TidalMarket = "DE",             // different non-credential field
            CacheDuration = 30              // different non-credential field
        };

        StubRuntime? r1 = await cache.GetAsync(s1);
        StubRuntime? r2 = await cache.GetAsync(s2);

        Assert.Same(r1, r2);            // cache hit — non-credential change ignored
        Assert.Equal(1, cache.BuildCount);
    }

    [Fact]
    public async Task IndexerAuthKey_ConfigPathChange_Rebuilds()
    {
        IndexerAuthKeyCache cache = new();
        TidalLidarrIndexerSettings s1 = new() { ConfigPath = "/tmp/tidal-a", RedirectUrl = "" };
        TidalLidarrIndexerSettings s2 = new() { ConfigPath = "/tmp/tidal-b", RedirectUrl = "" }; // new ConfigPath

        StubRuntime? r1 = await cache.GetAsync(s1);
        StubRuntime? r2 = await cache.GetAsync(s2);

        Assert.NotSame(r1, r2);
        Assert.Equal(2, cache.BuildCount);
    }

    // ---------------------------------------------------------------------------
    // D-12: one ServiceProvider per credential set (ConfigPath)
    //
    // T-2's cross-instance refresh-token rotation clobber was mitigated in-service
    // (RefreshOrClearOnRevokedAsync re-reads the store), but the STRUCTURAL fix is one
    // runtime/provider per credential set: indexer + download client + import list must
    // resolve the SAME singleton TidalOAuthService so refresh single-flight actually
    // covers all three host classes instead of three independent services racing over
    // one rotating-refresh-token file.
    //
    // RED evidence (2026-07-16, pre-collapse): with the three per-host-class caches,
    // TwoHostClasses_SameConfigPath_ShareOneOAuthService and
    // ImportList_SameConfigPath_JoinsTheSameProvider failed with
    // "Assert.Same() Failure: Values are not the same instance" — two distinct
    // TidalOAuthService instances over one ConfigPath.
    //
    // Tests construct isolated TidalRuntimeCache instances (internal ctor) so they never
    // leak state through TidalRuntimeCache.Shared.
    // ---------------------------------------------------------------------------

    private static string CreateTempConfigDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "tidalarr-d12-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }

    private static TidalLidarrIndexerSettings IndexerSettings(string configPath, string redirectUrl = "https://tidal.com/android/login/auth?code=abc&state=s1") =>
        new() { ConfigPath = configPath, RedirectUrl = redirectUrl };

    private static TidalLidarrDownloadClientSettings DownloadClientSettings(string configPath) =>
        new() { ConfigPath = configPath, DownloadPath = configPath };

    private static TidalFavoritesImportListSettings ImportListSettings(string configPath) =>
        new() { ConfigPath = configPath };

    [Fact]
    public async Task TwoHostClasses_SameConfigPath_ShareOneOAuthService()
    {
        TidalRuntimeCache cache = new();
        string dir = CreateTempConfigDir();
        try
        {
            TidalRuntime? idxRt = await cache.GetForIndexerAsync(IndexerSettings(dir));
            TidalRuntime? dcRt = await cache.GetForDownloadClientAsync(DownloadClientSettings(dir));

            Assert.NotNull(idxRt);
            Assert.NotNull(dcRt);

            // The whole point of D-12: one credential set (ConfigPath) => one provider =>
            // one TidalOAuthService => single-flight refresh covers both host classes.
            Assert.Same(idxRt, dcRt);
            ITidalAuth idxAuth = idxRt!.ServiceProvider.GetRequiredService<ITidalAuth>();
            ITidalAuth dcAuth = dcRt!.ServiceProvider.GetRequiredService<ITidalAuth>();
            Assert.Same(idxAuth, dcAuth);
        }
        finally
        {
            await cache.ResetAsync();
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task ImportList_SameConfigPath_JoinsTheSameProvider()
    {
        TidalRuntimeCache cache = new();
        string dir = CreateTempConfigDir();
        try
        {
            TidalRuntime? idxRt = await cache.GetForIndexerAsync(IndexerSettings(dir));
            TidalRuntime? ilRt = await cache.GetForImportListAsync(ImportListSettings(dir));

            Assert.NotNull(idxRt);
            Assert.NotNull(ilRt);

            Assert.Same(idxRt, ilRt);
            ITidalAuth idxAuth = idxRt!.ServiceProvider.GetRequiredService<ITidalAuth>();
            ITidalAuth ilAuth = ilRt!.ServiceProvider.GetRequiredService<ITidalAuth>();
            Assert.Same(idxAuth, ilAuth);
        }
        finally
        {
            await cache.ResetAsync();
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task DifferentConfigPaths_GetDistinctProviders()
    {
        TidalRuntimeCache cacheA = new();
        TidalRuntimeCache cacheB = new();
        string dirA = CreateTempConfigDir();
        string dirB = CreateTempConfigDir();
        try
        {
            TidalRuntime? rtA = await cacheA.GetForIndexerAsync(IndexerSettings(dirA));
            TidalRuntime? rtB = await cacheB.GetForDownloadClientAsync(DownloadClientSettings(dirB));

            Assert.NotNull(rtA);
            Assert.NotNull(rtB);

            // Different token files => different credential sets => distinct OAuth services.
            Assert.NotSame(rtA, rtB);
            ITidalAuth authA = rtA!.ServiceProvider.GetRequiredService<ITidalAuth>();
            ITidalAuth authB = rtB!.ServiceProvider.GetRequiredService<ITidalAuth>();
            Assert.NotSame(authA, authB);
        }
        finally
        {
            await cacheA.ResetAsync();
            await cacheB.ResetAsync();
            try { Directory.Delete(dirA, true); } catch { /* best-effort */ }
            try { Directory.Delete(dirB, true); } catch { /* best-effort */ }
        }
    }

    // Key-scheme pin: RedirectUrl is deliberately NOT part of the credential identity.
    // The token file is per-ConfigPath; the code exchange reads the pasted URL from HOST
    // settings per call; and TidalAuthTokenAuthAdapter ignores the provider-side
    // TidalCredentials value entirely. Rebuilding on RedirectUrl change (the old indexer
    // cache behavior) would split the indexer/download-client/import-list back onto
    // different providers whenever a redirect is pending — reintroducing the T-2 race.
    // The paste must instead flow as in-place sub-state on the registered settings.
    [Fact]
    public async Task RedirectUrlChange_SameConfigPath_ReusesProvider_AndUpdatesSubState()
    {
        TidalRuntimeCache cache = new();
        string dir = CreateTempConfigDir();
        try
        {
            const string firstPaste = "https://tidal.com/android/login/auth?code=first&state=s1";
            const string secondPaste = "https://tidal.com/android/login/auth?code=second&state=s2";

            TidalRuntime? rt1 = await cache.GetForIndexerAsync(IndexerSettings(dir, firstPaste));
            TidalRuntime? rt2 = await cache.GetForIndexerAsync(IndexerSettings(dir, secondPaste));

            Assert.NotNull(rt1);
            Assert.Same(rt1, rt2); // no rebuild on RedirectUrl change

            // ManagedTokenProvider.GetCredentials reads the registered TidalarrSettings at
            // call time, so the in-place update must be visible through the provider.
            TidalarrSettings registered = rt1!.ServiceProvider.GetRequiredService<TidalarrSettings>();
            Assert.Equal(secondPaste, registered.RedirectUrl);
        }
        finally
        {
            await cache.ResetAsync();
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }

    // The import list has no RedirectUrl input; its Apply must not clobber the indexer's
    // live credential sub-state with an empty string on every scheduled favorites fetch.
    [Fact]
    public async Task ImportListApply_DoesNotClobberIndexerRedirectUrl()
    {
        TidalRuntimeCache cache = new();
        string dir = CreateTempConfigDir();
        try
        {
            const string paste = "https://tidal.com/android/login/auth?code=live&state=s1";
            TidalRuntime? rt = await cache.GetForIndexerAsync(IndexerSettings(dir, paste));
            _ = await cache.GetForImportListAsync(ImportListSettings(dir));

            TidalarrSettings registered = rt!.ServiceProvider.GetRequiredService<TidalarrSettings>();
            Assert.Equal(paste, registered.RedirectUrl);
        }
        finally
        {
            await cache.ResetAsync();
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }

    // Download-client sub-state: even when the indexer built the provider first, the DC's
    // side fields (concurrency, lyrics toggles, quality) must reach the registered
    // TidalDownloadClientSettings instance the orchestrator/post-processor read.
    [Fact]
    public async Task DownloadClientApply_UpdatesRegisteredDownloadSettings_InPlace()
    {
        TidalRuntimeCache cache = new();
        string dir = CreateTempConfigDir();
        try
        {
            TidalRuntime? rt = await cache.GetForIndexerAsync(IndexerSettings(dir)); // indexer builds first

            TidalLidarrDownloadClientSettings dcSettings = DownloadClientSettings(dir);
            dcSettings.MaxConcurrentTrackDownloads = 3;
            dcSettings.SaveSyncedLyrics = false;
            _ = await cache.GetForDownloadClientAsync(dcSettings);

            TidalDownloadClientSettings registered = rt!.ServiceProvider.GetRequiredService<TidalDownloadClientSettings>();
            Assert.Equal(3, registered.MaxConcurrentTrackDownloads);
            Assert.False(registered.SaveSyncedLyrics);
        }
        finally
        {
            await cache.ResetAsync();
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }

    // Eviction/disposal parity: ResetAsync (and by extension the graveyard sweep, which
    // calls the same TidalRuntime.DisposeAsync) must actually dispose the merged
    // ServiceProvider so its HttpClient handler chains are torn down — same semantics the
    // three pre-D-12 runtime wrappers had.
    [Fact]
    public async Task ResetAsync_DisposesTheSharedServiceProvider()
    {
        TidalRuntimeCache cache = new();
        string dir = CreateTempConfigDir();
        try
        {
            TidalRuntime? rt = await cache.GetForIndexerAsync(IndexerSettings(dir));
            Assert.NotNull(rt);
            _ = rt!.ServiceProvider.GetRequiredService<ITidalAuth>(); // provider is live

            await cache.ResetAsync();

            _ = Assert.Throws<ObjectDisposedException>(() => rt.ServiceProvider.GetRequiredService<ITidalAuth>());
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* best-effort */ }
        }
    }
}
