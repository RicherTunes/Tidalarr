using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Integration;
using Lidarr.Plugin.Abstractions.Contracts;

namespace Tidalarr.Tests.Integration;

/// <summary>
/// Regression test for the provider-lifetime race condition.
///
/// Before the fix, <c>RebuildServiceProvider()</c> disposed the old <see cref="ServiceProvider"/>
/// while concurrent callers (e.g. <c>CreateIndexerAsync</c>) still held a reference to it,
/// resulting in <see cref="ObjectDisposedException"/>.
///
/// The fix makes <c>_serviceProvider</c> volatile and skips disposing the old provider during
/// swaps, letting the GC reclaim it after all references are released.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Area", "Concurrency")]
public class ProviderSwapConcurrencyTests
{
    private sealed class TestPluginContext : IPluginContext
    {
        public Version HostVersion { get; } = new(3, 1, 2, 4913);
        public ILoggerFactory LoggerFactory { get; } = NullLoggerFactory.Instance;
        public IServiceProvider? Services { get; } = null;
    }

    private static Dictionary<string, object?> MakeValidSettings(string suffix = "")
    {
        return new Dictionary<string, object?>
        {
            ["ConfigPath"] = Path.Combine(Path.GetTempPath(), $"tidalarr-test-{suffix}"),
            ["RedirectUrl"] = "https://tidal.com/android/login/auth",
            ["DownloadPath"] = Path.Combine(Path.GetTempPath(), $"tidalarr-dl-{suffix}"),
        };
    }

    /// <summary>
    /// Initializes a plugin, skipping when the host FluentValidation assembly is not available.
    /// The TidalModule registers FV validators during ConfigureServices, which requires FV 9.x
    /// from Docker-extracted host assemblies. Without them, the test cannot run.
    /// </summary>
    private static async Task<TidalarrPlugin> CreateInitializedPlugin()
    {
        TidalarrPlugin plugin = new();
        try
        {
            await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("FluentValidation"))
        {
            Skip.If(true, "FluentValidation host assembly not available (requires Docker-extracted host assemblies).");
        }

        return plugin;
    }

    [SkippableFact]
    [Trait("Area", "E2E/Hermetic")]
    public async Task SettingsApply_DisposesOldProvider_WhenNoAdapterScopeReferencesIt()
    {
        TidalarrPlugin plugin = await CreateInitializedPlugin();
        ServiceProvider oldProvider = GetCurrentRootProvider(plugin);

        PluginValidationResult applyResult = plugin.SettingsProvider.Apply(MakeValidSettings("retired-no-scope"));
        Assert.True(applyResult.IsValid, string.Join("; ", applyResult.Errors ?? []));

        AssertProviderDisposed(oldProvider);
        await plugin.DisposeAsync();
    }

    [SkippableFact]
    [Trait("Area", "E2E/Hermetic")]
    public async Task SettingsApply_DefersOldProviderDisposalUntilAdapterScopeIsDisposed()
    {
        TidalarrPlugin plugin = await CreateInitializedPlugin();
        ServiceProvider oldProvider = GetCurrentRootProvider(plugin);
        IIndexer? indexer = await plugin.CreateIndexerAsync(CancellationToken.None);

        PluginValidationResult applyResult = plugin.SettingsProvider.Apply(MakeValidSettings("retired-active-scope"));
        Assert.True(applyResult.IsValid, string.Join("; ", applyResult.Errors ?? []));

        AssertProviderUsable(oldProvider);

        Assert.NotNull(indexer);
        await indexer!.DisposeAsync();

        AssertProviderDisposed(oldProvider);
        await plugin.DisposeAsync();
    }

    [Fact]
    [Trait("Area", "E2E/Hermetic")]
    public void CreateAdapterOrDisposeScope_WhenAdapterConstructionThrows_DisposesScope()
    {
        RecordingScope scope = new();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            TidalarrPlugin.CreateAdapterOrDisposeScope<object>(
                scope,
                static _ => throw new InvalidOperationException("adapter resolution failed")));

        Assert.Equal("adapter resolution failed", ex.Message);
        Assert.True(scope.Disposed);
    }

    [SkippableFact]
    public async Task ConcurrentApplyAndCreateIndexer_DoesNotThrowObjectDisposedException()
    {
        // Arrange
        TidalarrPlugin plugin = await CreateInitializedPlugin();

        // Apply valid settings once so the plugin is in a working state
        plugin.SettingsProvider.Apply(MakeValidSettings("init"));

        // Act: hammer Apply and CreateIndexerAsync concurrently.
        // Before the fix, this would intermittently throw ObjectDisposedException
        // because Apply() disposed the old ServiceProvider while CreateIndexerAsync
        // was still resolving services from it.
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        const int iterations = 50;

        var tasks = new List<Task>();
        for (int i = 0; i < iterations; i++)
        {
            int capture = i;

            // Apply thread: rebuilds the service provider
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    plugin.SettingsProvider.Apply(MakeValidSettings($"apply-{capture}"));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));

            // Consumer thread: resolves services from the current provider
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await plugin.CreateIndexerAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // Expected if settings are not yet fully applied (validation failure)
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert: No ObjectDisposedException should have been thrown
        var disposed = exceptions.Where(e => e is ObjectDisposedException).ToList();
        Assert.Empty(disposed);
    }

    [SkippableFact]
    public async Task ConcurrentApplyAndCreateDownloadClient_DoesNotThrowObjectDisposedException()
    {
        // Arrange
        TidalarrPlugin plugin = await CreateInitializedPlugin();
        plugin.SettingsProvider.Apply(MakeValidSettings("init"));

        // Act: same pattern but with CreateDownloadClientAsync
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        const int iterations = 50;

        var tasks = new List<Task>();
        for (int i = 0; i < iterations; i++)
        {
            int capture = i;

            tasks.Add(Task.Run(() =>
            {
                try
                {
                    plugin.SettingsProvider.Apply(MakeValidSettings($"dl-{capture}"));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await plugin.CreateDownloadClientAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // Expected if settings are not yet fully applied
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var disposed = exceptions.Where(e => e is ObjectDisposedException).ToList();
        Assert.Empty(disposed);
    }

    private static ServiceProvider GetCurrentRootProvider(TidalarrPlugin plugin)
    {
        System.Reflection.FieldInfo? field = typeof(TidalarrPlugin).GetField(
            "_serviceProvider",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);

        ServiceProvider? provider = (ServiceProvider?)field!.GetValue(plugin);
        Assert.NotNull(provider);
        return provider!;
    }

    private static void AssertProviderUsable(ServiceProvider provider)
    {
        object? settings = provider.GetService(typeof(TidalarrSettings));
        Assert.NotNull(settings);
    }

    private static void AssertProviderDisposed(ServiceProvider provider)
    {
        Assert.Throws<ObjectDisposedException>(() => provider.GetService(typeof(TidalarrSettings)));
    }

    private sealed class RecordingScope : IServiceScope
    {
        public bool Disposed { get; private set; }
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
