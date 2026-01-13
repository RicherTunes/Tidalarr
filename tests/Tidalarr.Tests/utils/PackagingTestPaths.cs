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
        string? overridePath = Environment.GetEnvironmentVariable("TIDALARR_PACKAGE_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        string? repoRoot = TryFindRepoRoot();
        if (repoRoot == null)
        {
            return null;
        }

        string packageDir = Path.Combine(repoRoot, "src", "Tidalarr", "artifacts", "packages");
        if (!Directory.Exists(packageDir))
        {
            return null;
        }

        FileInfo? latest = new DirectoryInfo(packageDir)
            .GetFiles("*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return latest?.FullName;
    }

    public static string RequirePackagePath()
    {
        string? path = TryFindPackagePath();
        return path ?? throw new InvalidOperationException(
            "Tidalarr package not found. Run `./build.ps1 -Package -Configuration Release` " +
            "or set `TIDALARR_PACKAGE_PATH` to a package zip.");
    }

    public static string? TryFindRepoRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8 && current != null; i++, current = current.Parent)
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
        string? repoRoot = TryFindRepoRoot();
        if (repoRoot == null)
        {
            return null;
        }

        string path = Path.Combine(repoRoot, "docs", "PACKAGING_POLICY_BASELINE.md");
        return File.Exists(path) ? path : null;
    }

    public static ZipArchive OpenPackageZip(string packagePath)
    {
        return ZipFile.OpenRead(packagePath);
    }

    private static bool IsTruthy(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && (string.Equals(value, "1", StringComparison.Ordinal)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }
}

