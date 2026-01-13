namespace Tidalarr.Tests.Utils;

public sealed class HostVersionFactAttribute : FactAttribute
{
    public HostVersionFactAttribute()
    {
        bool strict = IsTruthy(Environment.GetEnvironmentVariable("CI"))
                     || IsTruthy(Environment.GetEnvironmentVariable("REQUIRE_HOST_VERSION_TESTS"));

        if (strict)
        {
            return;
        }

        string? repoRoot = PackagingTestPaths.TryFindRepoRoot();
        if (repoRoot == null)
        {
            Skip = "Repo root not found; host-version coupling tests disabled.";
            return;
        }

        string? hostDir = HostVersionTestPaths.TryFindHostAssembliesDir(repoRoot);
        if (hostDir == null)
        {
            Skip = "Host assemblies not found; set up ext/Lidarr/_output or set REQUIRE_HOST_VERSION_TESTS=true.";
            return;
        }

        string packagesProps = Path.Combine(repoRoot, "Directory.Packages.props");
        if (!File.Exists(packagesProps))
        {
            Skip = "Directory.Packages.props not found; host-version coupling tests disabled.";
        }
    }

    private static bool IsTruthy(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && (string.Equals(value, "1", StringComparison.Ordinal)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }
}

