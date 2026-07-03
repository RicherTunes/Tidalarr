using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Tidalarr.Tests.Utils;

namespace Tidalarr.Tests.Unit.Packaging;

public sealed class HostVersionCouplingTests
{
    [HostVersionFact]
    [Trait("Category", "Packaging")]
    public void DirectoryPackagesProps_Should_Match_HostVersions_For_Coupled_Dependencies()
    {
        string? repoRoot = PackagingTestPaths.TryFindRepoRoot();
        Assert.NotNull(repoRoot);

        string? hostDir = HostVersionTestPaths.TryFindHostAssembliesDir(repoRoot!);
        Assert.NotNull(hostDir);

        string packagesProps = Path.Combine(repoRoot!, "Directory.Packages.props");
        Assert.True(File.Exists(packagesProps), $"Expected {packagesProps} to exist.");

        // FluentValidation is internalized by ILRepack (PrivateAssets=all in Common) so it
        // does NOT need to match the host version. Only NLog crosses the host boundary.
        AssertPinnedMatchesHost(packagesProps, hostDir!, "NLog", "NLog.dll");
    }

    private static void AssertPinnedMatchesHost(string packagesPropsPath, string hostAssembliesDir, string packageId, string hostDllName)
    {
        string pinned = NormalizeVersion(ReadPinnedVersion(packagesPropsPath, packageId));
        Assert.False(string.IsNullOrWhiteSpace(pinned), $"Pinned version for {packageId} was not found in {packagesPropsPath}.");

        string hostDllPath = Path.Combine(hostAssembliesDir, hostDllName);
        if (!File.Exists(hostDllPath))
        {
            // Host directory exists (Lidarr.dll present) but this specific DLL is missing —
            // common in partial Docker extractions. Skip rather than fail.
            return;
        }

        var pinnedVersion = ParseVersion(pinned);
        Assert.NotNull(pinnedVersion);

        var hostAssemblyVersion = ReadHostAssemblyVersion(hostDllPath);
        Assert.NotNull(hostAssemblyVersion);

        // On Linux, FileVersionInfo can report NLog's AssemblyVersion (5.0.0.0)
        // instead of its package/file version (5.4.0). The load-bearing host
        // boundary is the AssemblyVersion major: NLog 6.x is unsafe with Lidarr's
        // NLog 5.x host assembly. Common's version-contract lint enforces the
        // exact package pin separately.
        Assert.Equal(hostAssemblyVersion!.Major, pinnedVersion!.Major);
    }

    private static string ReadPinnedVersion(string packagesPropsPath, string packageId)
    {
        XDocument doc = XDocument.Load(packagesPropsPath);
        string? version = doc.Descendants()
            .Where(e => e.Name.LocalName == "PackageVersion")
            .Where(e => string.Equals((string?)e.Attribute("Include"), packageId, StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("Version"))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return version ?? string.Empty;
    }

    private static Version? ReadHostAssemblyVersion(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string fullPath = new FileInfo(path).FullName;
        return AssemblyName.GetAssemblyName(fullPath).Version;
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Match match = Regex.Match(value, @"(\d+\.\d+\.\d+)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : value.Trim();
    }

    private static Version? ParseVersion(string value)
        => Version.TryParse(value, out var version) ? version : null;
}
