using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Lidarr.Plugin.Abstractions.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Plugin;

public sealed class TidalarrPluginLoadFixture : IAsyncLifetime
{
    private static readonly string[] DisallowedHostAssemblies =
    [
        "Lidarr.Core.dll",
        "Lidarr.Common.dll",
        "Lidarr.Host.dll"
    ];

    private PluginAssemblyLoadContext? loadContext;
    private string? pluginDirectory;
    private string? hostAssemblyDirectory;
    private WeakReference<Assembly>? pluginAssemblyReference;

    public IPlugin Plugin { get; private set; } = default!;
    public IServiceProvider Services { get; private set; } = default!;
    public IPluginContext PluginContext { get; private set; } = default!;
    public bool IsReady { get; private set; }
    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        string buildConfiguration = Environment.GetEnvironmentVariable("TIDALARR_TEST_CONFIGURATION") ?? "Debug";
        string targetFramework = Environment.GetEnvironmentVariable("TIDALARR_TEST_TFM") ?? "net8.0";
        string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string sourceDirectory = Path.Combine(solutionRoot, "src", "Tidalarr", "bin", buildConfiguration, targetFramework);

        if (!Directory.Exists(sourceDirectory))
        {
            SkipReason = $"Plugin output directory '{sourceDirectory}' not found. Build the plugin before running the smoke tests.";
            return;
        }

        string hostFallbackDirectory = Path.Combine(solutionRoot, "ext", "Lidarr", "_output", "net8.0");
        this.hostAssemblyDirectory = Directory.Exists(hostFallbackDirectory) ? hostFallbackDirectory : sourceDirectory;

        string pluginAssemblyName = "Lidarr.Plugin.Tidalarr.dll";
        string sourceAssemblyPath = Path.Combine(sourceDirectory, pluginAssemblyName);
        if (!File.Exists(sourceAssemblyPath))
        {
            SkipReason = $"Plugin assembly not found at '{sourceAssemblyPath}'. Build the plugin before running the smoke tests.";
            return;
        }

        this.pluginDirectory = Path.Combine(Path.GetTempPath(), $"tidalarr-smoke-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(this.pluginDirectory);

        foreach (string file in Directory.EnumerateFiles(sourceDirectory))
        {
            if (string.Equals(Path.GetFileName(file), "Lidarr.Plugin.Abstractions.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string destination = Path.Combine(this.pluginDirectory, Path.GetFileName(file));
            File.Copy(file, destination, overwrite: true);
        }

        ValidatePackagingMetadata(solutionRoot);

        string?[] disallowed = [.. Directory.EnumerateFiles(this.pluginDirectory, "*.dll")
            .Select(Path.GetFileName)
            .Where(name => name is not null && DisallowedHostAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))];

        if (disallowed.Length > 0)
        {
            SkipReason = $"Packaging copied host assemblies: {string.Join(", ", disallowed)}";
            return;
        }

        string pluginAssemblyPath = Path.Combine(this.pluginDirectory, pluginAssemblyName);
        this.loadContext = new PluginAssemblyLoadContext(pluginAssemblyPath, this.hostAssemblyDirectory);

        using (this.loadContext.EnterContextualReflection())
        {
            Assembly pluginAssembly = this.loadContext.LoadFromAssemblyPath(pluginAssemblyPath);
            this.pluginAssemblyReference = new WeakReference<Assembly>(pluginAssembly);

            TypeInfo pluginType = pluginAssembly.DefinedTypes.First(type => typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract);

            Plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
            PluginContext = new HarnessPluginContext();
            await Plugin.InitializeAsync(PluginContext, CancellationToken.None).ConfigureAwait(false);

            Dictionary<string, object?> settings = new()
            {
                ["ConfigPath"] = Path.GetTempPath(),
                ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=test",
                ["DownloadPath"] = Path.GetTempPath()
            };

            PluginValidationResult applied = Plugin.SettingsProvider.Apply(settings);
            if (!applied.IsValid)
            {
                SkipReason = $"Plugin settings failed validation: {string.Join(", ", applied.Errors)}";
                return;
            }
        }

        Services = ResolveServices(Plugin);
        IsReady = true;
    }

    public IServiceScope CreateScope()
    {
        MethodInfo? createScopeMethod = Plugin.GetType().GetMethod("CreateScope", BindingFlags.Instance | BindingFlags.NonPublic);
        return createScopeMethod is null
            ? throw new InvalidOperationException("Streaming plugin must expose CreateScope method.")
            : (IServiceScope)(createScopeMethod.Invoke(Plugin, []) ?? throw new InvalidOperationException("CreateScope returned null."));
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Services is IDisposable serviceProviderDisposable)
            {
                serviceProviderDisposable.Dispose();
            }

            if (Plugin is not null)
            {
                await Plugin.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            this.loadContext?.Unload();
            this.loadContext = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (this.pluginAssemblyReference is not null && this.pluginAssemblyReference.TryGetTarget(out _))
            {
                Console.WriteLine("SMOKE WARN: Plugin assembly persisted after unload. Check for static references.");
            }

            if (this.pluginDirectory is not null && Directory.Exists(this.pluginDirectory))
            {
                try
                {
                    Directory.Delete(this.pluginDirectory, recursive: true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            this.pluginDirectory = null;
            this.hostAssemblyDirectory = null;
            Plugin = default!;
            this.pluginAssemblyReference = null;
        }
    }

    private static void ValidatePackagingMetadata(string solutionRoot)
    {
        string packagesDirectory = Path.Combine(solutionRoot, "src", "Tidalarr", "artifacts", "packages");
        if (!Directory.Exists(packagesDirectory))
        {
            return;
        }

        string? metadataFile = Directory.EnumerateFiles(packagesDirectory, "*.metadata.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
        if (metadataFile is null || !File.Exists(metadataFile))
        {
            return;
        }

        string json = File.ReadAllText(metadataFile);
        PackagingMetadata? metadata = JsonSerializer.Deserialize<PackagingMetadata>(json);
        if (metadata is null)
        {
            return;
        }

        if (!File.Exists(metadata.HashPath))
        {
            throw new InvalidOperationException($"Expected hash file '{metadata.HashPath}' was not generated.");
        }

        string[] hostHits = [.. metadata.Assemblies.Where(name => DisallowedHostAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))];
        if (hostHits.Length > 0)
        {
            throw new InvalidOperationException($"Packaging metadata includes host assemblies: {string.Join(", ", hostHits)}");
        }
    }

    private static IServiceProvider ResolveServices(IPlugin plugin)
    {
        PropertyInfo? servicesProperty = plugin.GetType().GetProperty("Services", BindingFlags.Instance | BindingFlags.NonPublic);
        return servicesProperty?.GetValue(plugin) is IServiceProvider services
            ? services
            : throw new InvalidOperationException("Plugin must expose the internal Services property provided by StreamingPlugin.");
    }

    private sealed class PluginAssemblyLoadContext(string pluginAssemblyPath, string? hostAssemblyDirectory) : AssemblyLoadContext("Tidalarr.Tests.Plugin", isCollectible: true)
    {
        private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.Ordinal)
        {
            "Lidarr.Plugin.Abstractions",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions"
        };

        private static readonly HashSet<string> HostAssemblyNames = new(StringComparer.Ordinal)
        {
            "Lidarr.Core",
            "Lidarr.Common"
        };

        private readonly AssemblyDependencyResolver resolver = new(pluginAssemblyPath);
        private readonly string? hostAssemblyDirectory = hostAssemblyDirectory;

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyNameValue = assemblyName.Name;
            if (assemblyNameValue is not null)
            {
                if (SharedAssemblyNames.Contains(assemblyNameValue))
                {
                    return Default.LoadFromAssemblyName(assemblyName);
                }

                if (HostAssemblyNames.Contains(assemblyNameValue) && this.hostAssemblyDirectory is not null)
                {
                    string candidate = Path.Combine(this.hostAssemblyDirectory, assemblyNameValue + ".dll");
                    if (File.Exists(candidate))
                    {
                        return LoadFromAssemblyPath(candidate);
                    }
                }
            }

            string? path = this.resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = this.resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? base.LoadUnmanagedDll(unmanagedDllName) : LoadUnmanagedDllFromPath(path);
        }
    }
}

public sealed class HarnessPluginContext : IPluginContext
{
    public HarnessPluginContext()
    {
        ServiceCollection serviceCollection = new();
        _ = serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        Services = serviceCollection.BuildServiceProvider();
        LoggerFactory = Services.GetRequiredService<ILoggerFactory>();
    }

    public Version HostVersion { get; } = new(2, 14, 2, 4786);
    public ILoggerFactory LoggerFactory { get; }
    public IServiceProvider? Services { get; }
}

public sealed class TidalarrPluginSmokeTests(TidalarrPluginLoadFixture fixture) : IClassFixture<TidalarrPluginLoadFixture>
{
    private readonly TidalarrPluginLoadFixture fixture = fixture;

    [Fact]
    public void PluginLoadsAndProvidesServices()
    {
        // Fail explicitly if plugin isn't built - this catches build/load regressions
        // rather than silently passing when PluginPackagingDisable is set
        Assert.True(this.fixture.IsReady, this.fixture.SkipReason ?? "Plugin build not available - test cannot proceed.");

        Assert.NotNull(this.fixture.Plugin);
        Assert.NotNull(this.fixture.Services);

        Type? searchServiceType = this.fixture.Plugin.GetType().Assembly.GetType("Tidalarr.Application.Services.TidalSearchService");
        Assert.NotNull(searchServiceType);

        using IServiceScope scope = this.fixture.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService(searchServiceType!));
    }
}
