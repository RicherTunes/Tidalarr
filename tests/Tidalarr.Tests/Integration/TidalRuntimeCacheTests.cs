using System.Security.Cryptography;
using System.Text;
using Lidarr.Plugin.Common.HostBridge;
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
    // Auth-key semantics: non-credential fields must NOT trigger a rebuild
    // ---------------------------------------------------------------------------

    private sealed class IndexerAuthKeyCache : HostBridgeRuntimeCache<StubRuntime, TidalLidarrIndexerSettings>
    {
        public int BuildCount { get; private set; }

        protected override string ComputeAuthKey(TidalLidarrIndexerSettings settings)
        {
            string raw = $"{settings.ConfigPath}|{settings.RedirectUrl}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
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
            ConfigPath = "/tmp/tidal",      // same credential fields
            RedirectUrl = "https://example.com/cb",
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
}
