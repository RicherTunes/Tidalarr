using System.IO.Compression;
using System.Text.Json;
using Tidalarr.Tests.Utils;

namespace Tidalarr.Tests.Unit.Packaging;

public sealed class PluginPackagingPolicyTests
{
    private static PackagingPolicyBaseline Baseline =>
        PackagingPolicyBaseline.LoadOrDefault(PackagingTestPaths.TryFindPackagingPolicyBaselinePath());

    [PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Should_Contain_Required_Assemblies()
    {
        string packagePath = PackagingTestPaths.RequirePackagePath();
        using ZipArchive zip = PackagingTestPaths.OpenPackageZip(packagePath);
        HashSet<string> dlls = GetDllNames(zip);

        foreach (string required in Baseline.RequiredAssemblies)
        {
            Assert.Contains(required, dlls);
        }
    }

    [PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Should_Not_Contain_Forbidden_Assemblies()
    {
        string packagePath = PackagingTestPaths.RequirePackagePath();
        using ZipArchive zip = PackagingTestPaths.OpenPackageZip(packagePath);
        HashSet<string> dlls = GetDllNames(zip);

        foreach (string forbidden in Baseline.ForbiddenAssemblies)
        {
            Assert.DoesNotContain(forbidden, dlls);
        }

        // General host-leak guard: allow `Lidarr.Plugin.*` but reject other `Lidarr.*` / `NzbDrone.*`.
        string? hostLeak = dlls.FirstOrDefault(n =>
            (n.StartsWith("Lidarr.", StringComparison.OrdinalIgnoreCase)
             && !n.StartsWith("Lidarr.Plugin.", StringComparison.OrdinalIgnoreCase))
            || n.StartsWith("NzbDrone.", StringComparison.OrdinalIgnoreCase));

        Assert.True(hostLeak == null, $"package must not include host assemblies (Lidarr.* / NzbDrone.*): {hostLeak}");
    }

    [PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Should_Have_Reasonable_Size()
    {
        string packagePath = PackagingTestPaths.RequirePackagePath();
        long sizeBytes = new FileInfo(packagePath).Length;

        Assert.True(sizeBytes > 100_000, "a plugin package smaller than this likely indicates a packaging failure");
        Assert.True(sizeBytes < 15 * 1024 * 1024, "package bloat usually indicates an accidental dependency leak");
    }

    [PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Metadata_Should_Match_Contents()
    {
        string packagePath = PackagingTestPaths.RequirePackagePath();
        using ZipArchive zip = PackagingTestPaths.OpenPackageZip(packagePath);

        HashSet<string> dlls = GetDllNames(zip);

        // Verify plugin.json exists and references a valid main assembly
        PluginManifest pluginJson = ReadPluginJson(zip);
        Assert.False(string.IsNullOrWhiteSpace(pluginJson.Main), "plugin.json must specify a Main assembly");
        Assert.Contains(pluginJson.Main!, dlls, StringComparer.OrdinalIgnoreCase);

        // Verify the package contains at least one DLL (the main plugin assembly)
        Assert.NotEmpty(dlls);
    }

    private static HashSet<string> GetDllNames(ZipArchive zip)
    {
        return zip.Entries
            .Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static PluginManifest ReadPluginJson(ZipArchive zip)
    {
        ZipArchiveEntry? entry = zip.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName, "plugin.json", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(entry);

        using Stream stream = entry!.Open();
        PluginManifest? manifest = JsonSerializer.Deserialize<PluginManifest>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(manifest);
        return manifest!;
    }

    private sealed record PluginManifest(string? Main);
}
