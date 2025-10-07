using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Plugin;

public sealed class TidalarrPluginLoadFixture : IAsyncLifetime
{
    private static readonly string[] DisallowedHostAssemblies =
    {
        "Lidarr.Core.dll",
        "Lidarr.Common.dll",
        "Lidarr.Host.dll"
    };

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
        var buildConfiguration = Environment.GetEnvironmentVariable("TIDALARR_TEST_CONFIGURATION") ?? "Debug";
        var targetFramework = Environment.GetEnvironmentVariable("TIDALARR_TEST_TFM") ?? "net6.0";
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var sourceDirectory = Path.Combine(solutionRoot, "src", "Tidalarr", "bin", buildConfiguration, targetFramework);

        if (!Directory.Exists(sourceDirectory))
        {
            SkipReason = $"Plugin output directory '{sourceDirectory}' not found. Build the plugin before running the smoke tests.";
            return;
        }

        var hostFallbackDirectory = Path.Combine(solutionRoot, "ext", "Lidarr", "_output", "net6.0");
        hostAssemblyDirectory = Directory.Exists(hostFallbackDirectory) ? hostFallbackDirectory : sourceDirectory;

        var pluginAssemblyName = "Lidarr.Plugin.Tidalarr.dll";
        var sourceAssemblyPath = Path.Combine(sourceDirectory, pluginAssemblyName);
        if (!File.Exists(sourceAssemblyPath))
        {
            SkipReason = $"Plugin assembly not found at '{sourceAssemblyPath}'. Build the plugin before running the smoke tests.";
            return;
        }

        pluginDirectory = Path.Combine(Path.GetTempPath(), $"tidalarr-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            if (string.Equals(Path.GetFileName(file), "Lidarr.Plugin.Abstractions.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.Combine(pluginDirectory, Path.GetFileName(file));
            File.Copy(file, destination, overwrite: true);
        }

        ValidatePackagingMetadata(solutionRoot);

        var disallowed = Directory.EnumerateFiles(pluginDirectory, "*.dll")
            .Select(Path.GetFileName)
            .Where(name => name is not null && DisallowedHostAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (disallowed.Length > 0)
        {
            SkipReason = $"Packaging copied host assemblies: {string.Join(", ", disallowed)}";
            return;
        }

        var pluginAssemblyPath = Path.Combine(pluginDirectory, pluginAssemblyName);
        loadContext = new PluginAssemblyLoadContext(pluginAssemblyPath, hostAssemblyDirectory);

        using (loadContext.EnterContextualReflection())
        {
            var pluginAssembly = loadContext.LoadFromAssemblyPath(pluginAssemblyPath);
            pluginAssemblyReference = new WeakReference<Assembly>(pluginAssembly);

            var pluginType = pluginAssembly.DefinedTypes.First(type => typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract);

            Plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
            PluginContext = new HarnessPluginContext();
            await Plugin.InitializeAsync(PluginContext, CancellationToken.None).ConfigureAwait(false);

            var settings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = Path.GetTempPath(),
                ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=test",
                ["DownloadPath"] = Path.GetTempPath()
            };

            var applied = Plugin.SettingsProvider.Apply(settings);
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
        var createScopeMethod = Plugin.GetType().GetMethod("CreateScope", BindingFlags.Instance | BindingFlags.NonPublic);
        if (createScopeMethod is null)
        {
            throw new InvalidOperationException("Streaming plugin must expose CreateScope method.");
        }

        return (IServiceScope)(createScopeMethod.Invoke(Plugin, Array.Empty<object?>()) ?? throw new InvalidOperationException("CreateScope returned null."));
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
            loadContext?.Unload();
            loadContext = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (pluginAssemblyReference is not null && pluginAssemblyReference.TryGetTarget(out _))
            {
                Console.WriteLine("SMOKE WARN: Plugin assembly persisted after unload. Check for static references.");
            }

            if (pluginDirectory is not null && Directory.Exists(pluginDirectory))
            {
                try
                {
                    Directory.Delete(pluginDirectory, recursive: true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            pluginDirectory = null;
            hostAssemblyDirectory = null;
            Plugin = default!;
            pluginAssemblyReference = null;
        }
    }

    private static void ValidatePackagingMetadata(string solutionRoot)
    {
        var packagesDirectory = Path.Combine(solutionRoot, "src", "Tidalarr", "artifacts", "packages");
        if (!Directory.Exists(packagesDirectory))
        {
            return;
        }

        var metadataFile = Directory.EnumerateFiles(packagesDirectory, "*.metadata.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
        if (metadataFile is null || !File.Exists(metadataFile))
        {
            return;
        }

        var json = File.ReadAllText(metadataFile);
        var metadata = JsonSerializer.Deserialize<PackagingMetadata>(json);
        if (metadata is null)
        {
            return;
        }

        if (!File.Exists(metadata.HashPath))
        {
            throw new InvalidOperationException($"Expected hash file '{metadata.HashPath}' was not generated.");
        }

        var hostHits = metadata.Assemblies
            .Where(name => DisallowedHostAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (hostHits.Length > 0)
        {
            throw new InvalidOperationException($"Packaging metadata includes host assemblies: {string.Join(", ", hostHits)}");
        }
    }

    private static IServiceProvider ResolveServices(IPlugin plugin)
    {
        var servicesProperty = plugin.GetType().GetProperty("Services", BindingFlags.Instance | BindingFlags.NonPublic);
        if (servicesProperty?.GetValue(plugin) is IServiceProvider services)
        {
            return services;
        }

        throw new InvalidOperationException("Plugin must expose the internal Services property provided by StreamingPlugin.");
    }

    private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
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

        private readonly AssemblyDependencyResolver resolver;
        private readonly string? hostAssemblyDirectory;

        public PluginAssemblyLoadContext(string pluginAssemblyPath, string? hostAssemblyDirectory)
            : base("Tidalarr.Tests.Plugin", isCollectible: true)
        {
            resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
            this.hostAssemblyDirectory = hostAssemblyDirectory;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyNameValue = assemblyName.Name;
            if (assemblyNameValue is not null)
            {
                if (SharedAssemblyNames.Contains(assemblyNameValue))
                {
                    return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
                }

                if (HostAssemblyNames.Contains(assemblyNameValue) && hostAssemblyDirectory is not null)
                {
                    var candidate = Path.Combine(hostAssemblyDirectory, assemblyNameValue + ".dll");
                    if (File.Exists(candidate))
                    {
                        return LoadFromAssemblyPath(candidate);
                    }
                }
            }

            var path = resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? base.LoadUnmanagedDll(unmanagedDllName) : LoadUnmanagedDllFromPath(path);
        }
    }
}

public sealed class HarnessPluginContext : IPluginContext
{
    private readonly ILoggerFactory loggerFactory;

    public HarnessPluginContext()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        Services = serviceCollection.BuildServiceProvider();
        loggerFactory = Services.GetRequiredService<ILoggerFactory>();
    }

    public Version HostVersion { get; } = new(2, 14, 2, 4786);
    public ILoggerFactory LoggerFactory => loggerFactory;
    public IServiceProvider? Services { get; }
}

public sealed class TidalarrPluginSmokeTests : IClassFixture<TidalarrPluginLoadFixture>
{
    private readonly TidalarrPluginLoadFixture fixture;

    public TidalarrPluginSmokeTests(TidalarrPluginLoadFixture fixture) => this.fixture = fixture;

    [Fact]
    public void PluginLoadsAndProvidesServices()
    {
        Assert.True(fixture.IsReady, fixture.SkipReason ?? "Plugin build not available.");

        Assert.NotNull(fixture.Plugin);
        Assert.NotNull(fixture.Services);

        var searchServiceType = fixture.Plugin.GetType().Assembly.GetType("Tidalarr.Application.Services.TidalSearchService");
        Assert.NotNull(searchServiceType);

        using var scope = fixture.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService(searchServiceType!));
    }
}
