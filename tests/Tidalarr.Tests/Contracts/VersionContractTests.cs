using System.IO;
using System.Text.Json;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Contracts;

/// <summary>
/// Catches version drift between sources of truth: VERSION file, plugin.json,
/// TidalModule.Version constant, and assembly metadata.
///
/// Background: TidalModule.Version was a hardcoded `const string` literal that rotted
/// from "1.0.1" → "1.1.0" while VERSION/plugin.json moved to 1.1.1. Three test files
/// (EndToEndIntegrationTests, TidalarrPluginCovTests, TidalarrPluginComplianceTests)
/// still asserted the literal "1.0.1" long after it was inaccurate. Lifting the version
/// derivation to assembly metadata and adding this contract test prevents the next bump
/// from silently rotting the same way.
/// </summary>
public class VersionContractTests
{
    [Fact]
    public void TidalModuleVersion_MatchesPluginJsonVersion()
    {
        var pluginJsonPath = LocatePluginJson();
        Skip.If(pluginJsonPath is null, "plugin.json not found in baseDir or repo root");

        using var doc = JsonDocument.Parse(File.ReadAllText(pluginJsonPath!));
        var expected = doc.RootElement.GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(expected), "plugin.json must declare a version");

        Assert.Equal(expected, TidalModule.Version);
    }

    [Fact]
    public void TidalModuleVersion_MatchesAssemblyVersion()
    {
        var asmVersion = typeof(TidalModule).Assembly.GetName().Version?.ToString(3);
        Assert.False(string.IsNullOrWhiteSpace(asmVersion), "Tidalarr assembly must declare a version");
        Assert.Equal(asmVersion, TidalModule.Version);
    }

    [Fact]
    public void VersionFile_MatchesPluginJsonVersion()
    {
        var versionPath = LocateRepoFile("VERSION");
        var pluginJsonPath = LocatePluginJson();
        Skip.If(versionPath is null || pluginJsonPath is null,
            "VERSION or plugin.json not found — only enforced for repo-rooted runs");

        var versionFile = File.ReadAllText(versionPath!).Trim();
        using var doc = JsonDocument.Parse(File.ReadAllText(pluginJsonPath!));
        var pluginJson = doc.RootElement.GetProperty("version").GetString();

        Assert.Equal(versionFile, pluginJson);
    }

    private static string? LocatePluginJson()
    {
        // 1. AppContext.BaseDirectory — copied here by the SDK at build time
        var candidate = Path.Combine(AppContext.BaseDirectory, "plugin.json");
        if (File.Exists(candidate)) return candidate;

        // 2. Walk up from BaseDirectory to repo root
        return LocateRepoFile("plugin.json");
    }

    private static string? LocateRepoFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
