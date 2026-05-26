using System.IO.Compression;
using Lidarr.Plugin.Common.TestKit.Packaging;

namespace Tidalarr.Tests.Utils;

/// <summary>
/// Plugin-specific packaging path helpers — thin wrapper over the shared
/// <see cref="Lidarr.Plugin.Common.TestKit.Packaging.PackagingTestPaths"/> factory.
/// </summary>
public static class PackagingTestPaths
{
    private static readonly Lidarr.Plugin.Common.TestKit.Packaging.PackagingTestPaths _paths =
        Lidarr.Plugin.Common.TestKit.Packaging.PackagingTestPaths.For("Tidalarr");

    public static bool IsStrictMode() =>
        Lidarr.Plugin.Common.TestKit.Packaging.PackagingTestPaths.IsStrictMode();

    public static string? TryFindPackagePath() => _paths.TryFindPackagePath();

    public static string RequirePackagePath() => _paths.RequirePackagePath();

    public static string? TryFindRepoRoot() => _paths.TryFindRepoRoot();

    public static string FindRepoRootOrThrow() => _paths.FindRepoRootOrThrow();

    public static string? TryFindPackagingPolicyBaselinePath() =>
        _paths.TryFindPackagingPolicyBaselinePath();

    public static ZipArchive OpenPackageZip(string packagePath) =>
        Lidarr.Plugin.Common.TestKit.Packaging.PackagingTestPaths.OpenPackageZip(packagePath);
}
