using System.IO.Compression;

namespace Tidalarr.Tests.Utils;

public static class PackagingTestPaths
{
    public static bool IsStrictMode()
    {
        return IsTruthy(Environment.GetEnvironmentVariable("CI"))
               || IsTruthy(Environment.GetEnvironmentVariable("REQUIRE_PACKAGE_TESTS"));
    }

    public static string? TryFindPackagePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("TIDALARR_PACKAGE_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var repoRoot = TryFindRepoRoot();
        if (repoRoot == null)
        {
            return null;
        }

        var packageDir = Path.Combine(repoRoot, "src", "Tidalarr", "artifacts", "packages");
        if (!Directory.Exists(packageDir))
        {
            return null;
        }

        var latest = new DirectoryInfo(packageDir)
            .GetFiles("*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return latest?.FullName;
    }

    public static string RequirePackagePath()
    {
        var path = TryFindPackagePath();
        if (path != null)
        {
            return path;
        }

        throw new InvalidOperationException(
            "Tidalarr package not found. Run `./build.ps1 -Package -Configuration Release` " +
            "or set `TIDALARR_PACKAGE_PATH` to a package zip.");
    }

    public static string? TryFindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 8 && current != null; i++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Tidalarr.sln")))
            {
                return current.FullName;
            }
        }

        return null;
    }

    public static string? TryFindPackagingPolicyBaselinePath()
    {
        var repoRoot = TryFindRepoRoot();
        if (repoRoot == null)
        {
            return null;
        }

        var path = Path.Combine(repoRoot, "docs", "PACKAGING_POLICY_BASELINE.md");
        return File.Exists(path) ? path : null;
    }

    public static ZipArchive OpenPackageZip(string packagePath) => ZipFile.OpenRead(packagePath);

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "1", StringComparison.Ordinal)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

