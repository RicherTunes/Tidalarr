using System.IO.Compression;
using System.Text.Json;
using Tidalarr.Tests.Utils;

namespace Tidalarr.Tests.Unit.Packaging;

public sealed class PluginPackagingPolicyTests
{
    private static PackagingPolicyBaseline Baseline =>
        PackagingPolicyBaseline.LoadOrDefault(PackagingTestPaths.TryFindPackagingPolicyBaselinePath());

    [Utils.PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Should_Contain_Required_Assemblies()
    {
        var packagePath = PackagingTestPaths.RequirePackagePath();
        using var zip = PackagingTestPaths.OpenPackageZip(packagePath);
        var dlls = GetDllNames(zip);

        foreach (var required in Baseline.RequiredAssemblies)
        {
            Assert.Contains(required, dlls);
        }
    }

    [Utils.PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Should_Not_Contain_Forbidden_Assemblies()
    {
        var packagePath = PackagingTestPaths.RequirePackagePath();
        using var zip = PackagingTestPaths.OpenPackageZip(packagePath);
        var dlls = GetDllNames(zip);

        foreach (var forbidden in Baseline.ForbiddenAssemblies)
        {
            Assert.DoesNotContain(forbidden, dlls);
        }

        // General host-leak guard: allow `Lidarr.Plugin.*` but reject other `Lidarr.*` / `NzbDrone.*`.
        var hostLeak = dlls.FirstOrDefault(n =>
            (n.StartsWith("Lidarr.", StringComparison.OrdinalIgnoreCase)
             && !n.StartsWith("Lidarr.Plugin.", StringComparison.OrdinalIgnoreCase))
            || n.StartsWith("NzbDrone.", StringComparison.OrdinalIgnoreCase));

        Assert.True(hostLeak == null, $"package must not include host assemblies (Lidarr.* / NzbDrone.*): {hostLeak}");
    }

    [Utils.PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Should_Have_Reasonable_Size()
    {
        var packagePath = PackagingTestPaths.RequirePackagePath();
        var sizeBytes = new FileInfo(packagePath).Length;

        Assert.True(sizeBytes > 100_000, "a plugin package smaller than this likely indicates a packaging failure");
        Assert.True(sizeBytes < 15 * 1024 * 1024, "package bloat usually indicates an accidental dependency leak");
    }

    [Utils.PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Metadata_Should_Match_Contents()
    {
        var packagePath = PackagingTestPaths.RequirePackagePath();
        using var zip = PackagingTestPaths.OpenPackageZip(packagePath);

        var dlls = GetDllNames(zip);
        var metadata = ReadPackageMetadata(zip);

        Assert.NotNull(metadata.Assemblies);
        Assert.NotEmpty(metadata.Assemblies);

        var metadataNames = metadata.Assemblies
            .Select(a => a.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(dlls.Count, metadataNames.Count);
        foreach (var dll in dlls)
        {
            Assert.Contains(dll, metadataNames);
        }

        var pluginJson = ReadPluginJson(zip);
        Assert.False(string.IsNullOrWhiteSpace(pluginJson.Main));
        Assert.Contains(pluginJson.Main!, dlls);
    }

    private static HashSet<string> GetDllNames(ZipArchive zip)
    {
        return zip.Entries
            .Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static PackageMetadata ReadPackageMetadata(ZipArchive zip)
    {
        var entry = zip.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName, "package-metadata.json", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(entry);

        using var stream = entry!.Open();
        var metadata = JsonSerializer.Deserialize<PackageMetadata>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(metadata);
        return metadata!;
    }

    private static PluginManifest ReadPluginJson(ZipArchive zip)
    {
        var entry = zip.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName, "plugin.json", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(entry);

        using var stream = entry!.Open();
        var manifest = JsonSerializer.Deserialize<PluginManifest>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(manifest);
        return manifest!;
    }

    private sealed record PackageMetadata(IReadOnlyList<PackageAssembly> Assemblies);

    private sealed record PackageAssembly(string Name);

    private sealed record PluginManifest(string? Main);
}
