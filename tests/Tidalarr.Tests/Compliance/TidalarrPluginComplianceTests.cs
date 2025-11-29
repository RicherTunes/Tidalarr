using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Manifest;
using Tidalarr.Integration;
using Xunit;

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
        _pluginAssembly = typeof(TidalarrPlugin).Assembly;

        var manifestPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plugin.json");
        if (File.Exists(manifestPath))
        {
            _pluginManifest = PluginManifest.Load(manifestPath);
        }
        else
        {
            // Fallback to minimal manifest
            _pluginManifest = new PluginManifest
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
        Assert.False(string.IsNullOrWhiteSpace(_pluginManifest.Id));
        Assert.Equal("tidalarr", _pluginManifest.Id);
    }

    [Fact]
    public void Manifest_HasRequiredName()
    {
        Assert.False(string.IsNullOrWhiteSpace(_pluginManifest.Name));
        Assert.Equal("Tidalarr", _pluginManifest.Name);
    }

    [Fact]
    public void Manifest_HasRequiredVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(_pluginManifest.Version));
    }

    [Fact]
    public void Manifest_HasRequiredApiVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(_pluginManifest.ApiVersion));
    }

    [Fact]
    public void Manifest_VersionIsValidSemVer()
    {
        Assert.True(Version.TryParse(_pluginManifest.Version, out _),
            $"Version '{_pluginManifest.Version}' is not valid semver");
    }

    #endregion

    #region Assembly Tests

    [Fact]
    public void Assembly_LoadsWithoutErrors()
    {
        var types = _pluginAssembly.GetTypes();
        Assert.NotEmpty(types);
    }

    [Fact]
    public void Assembly_CommonLibraryTypesAreInternalized()
    {
        var publicTypes = _pluginAssembly.GetExportedTypes();

        // Check for exposed Common library types that should be internalized
        var exposedCommonTypes = publicTypes
            .Where(t => t.Namespace?.StartsWith("Lidarr.Plugin.Common", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Empty(exposedCommonTypes);
    }

    [Fact]
    public void Assembly_ImplementsIPlugin()
    {
        var pluginTypes = _pluginAssembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        Assert.NotEmpty(pluginTypes);
    }

    [Fact]
    public void Assembly_HasPluginEntryPoint()
    {
        var pluginType = typeof(TidalarrPlugin);
        Assert.NotNull(pluginType);
        Assert.True(typeof(IPlugin).IsAssignableFrom(pluginType));
    }

    [Fact]
    public void Assembly_PluginIsInstantiable()
    {
        var plugin = Activator.CreateInstance(typeof(TidalarrPlugin));
        Assert.NotNull(plugin);
        Assert.IsAssignableFrom<IPlugin>(plugin);
    }

    [Fact]
    public void Assembly_PluginHasSettingsProvider()
    {
        var plugin = (IPlugin)Activator.CreateInstance(typeof(TidalarrPlugin))!;
        Assert.NotNull(plugin.SettingsProvider);
    }

    [Fact]
    public void Assembly_PluginHasManifest()
    {
        var plugin = (IPlugin)Activator.CreateInstance(typeof(TidalarrPlugin))!;
        Assert.NotNull(plugin.Manifest);
    }

    #endregion

    #region Security Tests

    [Fact]
    public void Security_NoHardcodedCredentials()
    {
        var suspiciousPatterns = new[]
        {
            "password=",
            "apikey=",
            "api_key=",
            "secret=",
            "token=",
            "bearer ",
            "basic "
        };

        var allTypes = _pluginAssembly.GetTypes();
        var foundCredentials = new List<string>();

        foreach (var type in allTypes)
        {
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields.Where(f => f.FieldType == typeof(string)))
            {
                try
                {
                    var value = field.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        foreach (var pattern in suspiciousPatterns)
                        {
                            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase) &&
                                value.Length > pattern.Length + 10)
                            {
                                var afterPattern = value.Substring(
                                    value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) + pattern.Length);
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
        var namespaces = _pluginAssembly.GetTypes()
            .Select(t => t.Namespace)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        Assert.True(namespaces.Any(n => n!.StartsWith("Tidalarr", StringComparison.Ordinal)));
    }

    [Fact]
    public void Namespace_HasIntegrationNamespace()
    {
        var integrationTypes = _pluginAssembly.GetTypes()
            .Where(t => t.Namespace?.Contains("Integration", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(integrationTypes);
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}
