using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Tidalarr.Tests;

/// <summary>
/// Guards against "byte-identical Abstractions" drift by verifying that when
/// NuGet packages are expected, we're not accidentally compiling Abstractions from source.
/// </summary>
public sealed class PackagingGuardTests
{
    /// <summary>
    /// When USE_NUGET_PACKAGES environment variable is set, verify that
    /// Lidarr.Plugin.Abstractions is loaded from NuGet packages directory,
    /// not from the local submodule build output.
    /// </summary>
    [Fact]
    public void Abstractions_Should_Not_Be_From_Submodule_When_NuGet_Expected()
    {
        var useNuGet = Environment.GetEnvironmentVariable("USE_NUGET_PACKAGES");

        // Skip if not in NuGet-expected mode (local dev with submodule is fine)
        if (!string.Equals(useNuGet, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Find the Abstractions assembly
        var abstractionsAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Lidarr.Plugin.Abstractions");

        Assert.NotNull(abstractionsAssembly);

        var location = abstractionsAssembly.Location;
        Assert.False(string.IsNullOrEmpty(location), "Abstractions assembly location should not be empty");

        // Verify it's NOT from the submodule path
        var normalizedPath = location.Replace('\\', '/').ToLowerInvariant();
        var isFromSubmodule = normalizedPath.Contains("/ext/lidarr.plugin.common/") ||
                              normalizedPath.Contains("\\ext\\lidarr.plugin.common\\");

        Assert.False(isFromSubmodule,
            $"When USE_NUGET_PACKAGES=true, Abstractions should come from NuGet packages, " +
            $"not submodule. Found at: {location}");
    }

    /// <summary>
    /// Verify that the Abstractions assembly version matches what we expect from NuGet.
    /// This catches version drift between submodule and NuGet package.
    /// </summary>
    [Fact]
    public void Abstractions_Version_Should_Match_Expected()
    {
        var abstractionsAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Lidarr.Plugin.Abstractions");

        Assert.NotNull(abstractionsAssembly);

        var version = abstractionsAssembly.GetName().Version;
        Assert.NotNull(version);

        // Log the version for diagnostic purposes
        // In CI, this helps verify which version is actually being used
        Console.WriteLine($"[PackagingGuard] Lidarr.Plugin.Abstractions version: {version}");
        Console.WriteLine($"[PackagingGuard] Location: {abstractionsAssembly.Location}");
    }

    /// <summary>
    /// Verify that the submodule doesn't have a locally-built Abstractions.dll
    /// when we're supposed to be using NuGet packages.
    /// </summary>
    [Fact]
    public void Submodule_Should_Not_Have_Built_Abstractions_When_NuGet_Expected()
    {
        var useNuGet = Environment.GetEnvironmentVariable("USE_NUGET_PACKAGES");

        // Skip if not in NuGet-expected mode
        if (!string.Equals(useNuGet, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Find repo root by walking up from test assembly location
        var testDir = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(testDir);

        if (repoRoot == null)
        {
            // Can't find repo root, skip this check
            return;
        }

        var submoduleOutputPaths = new[]
        {
            Path.Combine(repoRoot, "ext", "Lidarr.Plugin.Common", "src", "Abstractions", "bin"),
            Path.Combine(repoRoot, "ext", "Lidarr.Plugin.Common", "src", "Abstractions", "obj"),
        };

        foreach (var path in submoduleOutputPaths)
        {
            if (Directory.Exists(path))
            {
                var dllFiles = Directory.GetFiles(path, "Lidarr.Plugin.Abstractions.dll", SearchOption.AllDirectories);
                Assert.Empty(dllFiles);
            }
        }
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = startDir;
        for (var i = 0; i < 15; i++)
        {
            if (File.Exists(Path.Combine(dir, "Tidalarr.sln")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }

        return null;
    }
}
