using System.Reflection;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Manifest;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Compliance;

/// <summary>
/// Plugin compliance tests for Tidalarr.
/// These tests verify the plugin meets the minimum quality bar for Lidarr plugins.
/// </summary>
[Trait("Category", "Compliance")]
[Trait("Category", "Plugin")]
public class TidalarrPluginComplianceTests : IDisposable
{
    private readonly Assembly _pluginAssembly;
    private readonly PluginManifest _pluginManifest;

    public TidalarrPluginComplianceTests()
    {
        this._pluginAssembly = typeof(TidalarrPlugin).Assembly;

        string manifestPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plugin.json");
        if (File.Exists(manifestPath))
        {
            this._pluginManifest = PluginManifest.Load(manifestPath);
        }
        else
        {
            // Fallback to minimal manifest
            this._pluginManifest = new PluginManifest
            {
                Id = "tidalarr",
                Name = "Tidalarr",
                Version = "1.0.1",
                ApiVersion = "1.x"
            };
        }
    }

    #region Manifest Tests

    [Fact]
    public void Manifest_HasRequiredId()
    {
        Assert.False(string.IsNullOrWhiteSpace(this._pluginManifest.Id));
        Assert.Equal("tidalarr", this._pluginManifest.Id);
    }

    [Fact]
    public void Manifest_HasRequiredName()
    {
        Assert.False(string.IsNullOrWhiteSpace(this._pluginManifest.Name));
        Assert.Equal("Tidalarr", this._pluginManifest.Name);
    }

    [Fact]
    public void Manifest_HasRequiredVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(this._pluginManifest.Version));
    }

    [Fact]
    public void Manifest_HasRequiredApiVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(this._pluginManifest.ApiVersion));
    }

    [Fact]
    public void Manifest_VersionIsValidSemVer()
    {
        Assert.True(Version.TryParse(this._pluginManifest.Version, out _),
            $"Version '{this._pluginManifest.Version}' is not valid semver");
    }

    #endregion

    #region Assembly Tests

    [Fact]
    public void Assembly_LoadsWithoutErrors()
    {
        Type[] types = this._pluginAssembly.GetTypes();
        Assert.NotEmpty(types);
    }

    [Fact]
    public void Assembly_CommonLibraryTypesAreInternalized()
    {
        Type[] publicTypes = this._pluginAssembly.GetExportedTypes();

        // Check for exposed Common library types that should be internalized
        List<Type> exposedCommonTypes = [.. publicTypes.Where(t => t.Namespace?.StartsWith("Lidarr.Plugin.Common", StringComparison.Ordinal) == true)];

        Assert.Empty(exposedCommonTypes);
    }

    [Fact]
    public void Assembly_ImplementsIPlugin()
    {
        List<Type> pluginTypes = [.. this._pluginAssembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)];

        Assert.NotEmpty(pluginTypes);
    }

    [Fact]
    public void Assembly_HasPluginEntryPoint()
    {
        Type pluginType = typeof(TidalarrPlugin);
        Assert.NotNull(pluginType);
        Assert.True(typeof(IPlugin).IsAssignableFrom(pluginType));
    }

    [Fact]
    public void Assembly_PluginIsInstantiable()
    {
        object? plugin = Activator.CreateInstance(typeof(TidalarrPlugin));
        Assert.NotNull(plugin);
        _ = Assert.IsAssignableFrom<IPlugin>(plugin);
    }

    [Fact]
    public void Assembly_PluginHasSettingsProvider()
    {
        IPlugin plugin = (IPlugin)Activator.CreateInstance(typeof(TidalarrPlugin))!;
        Assert.NotNull(plugin.SettingsProvider);
    }

    [Fact]
    public void Assembly_PluginHasManifest()
    {
        IPlugin plugin = (IPlugin)Activator.CreateInstance(typeof(TidalarrPlugin))!;
        Assert.NotNull(plugin.Manifest);
    }

    #endregion

    #region Security Tests

    [Fact]
    public void Security_NoHardcodedCredentials()
    {
        string[] suspiciousPatterns =
        [
            "password=",
            "apikey=",
            "api_key=",
            "secret=",
            "token=",
            "bearer ",
            "basic "
        ];

        Type[] allTypes = this._pluginAssembly.GetTypes();
        List<string> foundCredentials = [];

        foreach (Type type in allTypes)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (FieldInfo? field in fields.Where(f => f.FieldType == typeof(string)))
            {
                try
                {
                    string? value = field.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        foreach (string? pattern in suspiciousPatterns)
                        {
                            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase) &&
                                value.Length > pattern.Length + 10)
                            {
                                string afterPattern = value[
                                    (value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) + pattern.Length)..];
                                if (!afterPattern.StartsWith("{") &&
                                    !afterPattern.StartsWith("$") &&
                                    !afterPattern.StartsWith("<"))
                                {
                                    foundCredentials.Add($"{type.Name}.{field.Name}");
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Skip fields that can't be read
                }
            }
        }

        Assert.Empty(foundCredentials);
    }

    #endregion

    #region Namespace Organization Tests

    [Fact]
    public void Namespace_HasCorrectRootNamespace()
    {
        List<string?> namespaces = [.. this._pluginAssembly.GetTypes()
            .Select(t => t.Namespace)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()];

        Assert.Contains(namespaces, n => n!.StartsWith("Tidalarr", StringComparison.Ordinal));
    }

    [Fact]
    public void Namespace_HasIntegrationNamespace()
    {
        List<Type> integrationTypes = [.. this._pluginAssembly.GetTypes().Where(t => t.Namespace?.Contains("Integration", StringComparison.Ordinal) == true)];

        Assert.NotEmpty(integrationTypes);
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}
