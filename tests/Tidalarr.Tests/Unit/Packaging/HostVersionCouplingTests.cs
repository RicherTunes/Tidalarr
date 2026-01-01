using System.Diagnostics;
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

        AssertPinnedMatchesHost(packagesProps, hostDir!, "FluentValidation", "FluentValidation.dll");
        AssertPinnedMatchesHost(packagesProps, hostDir!, "NLog", "NLog.dll");
    }

    private static void AssertPinnedMatchesHost(string packagesPropsPath, string hostAssembliesDir, string packageId, string hostDllName)
    {
        string pinned = NormalizeVersion(ReadPinnedVersion(packagesPropsPath, packageId));
        Assert.False(string.IsNullOrWhiteSpace(pinned), $"Pinned version for {packageId} was not found in {packagesPropsPath}.");

        string hostFileVersion = NormalizeVersion(ReadHostFileVersion(Path.Combine(hostAssembliesDir, hostDllName)));
        Assert.False(string.IsNullOrWhiteSpace(hostFileVersion), $"Host version for {hostDllName} was not found in {hostAssembliesDir}.");

        Assert.Equal(hostFileVersion, pinned);
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

    private static string ReadHostFileVersion(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        string fullPath = new FileInfo(path).FullName;
        return FileVersionInfo.GetVersionInfo(fullPath).FileVersion ?? string.Empty;
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
}

