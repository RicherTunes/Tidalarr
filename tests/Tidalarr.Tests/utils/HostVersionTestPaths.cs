namespace Tidalarr.Tests.Utils;

public static class HostVersionTestPaths
{
    public static string? TryFindHostAssembliesDir(string repoRoot)
    {
        string[] candidates =
        [
            Path.Combine(repoRoot, "ext", "Lidarr", "_output", "net8.0"),
            Path.Combine(repoRoot, "ext", "Lidarr-docker", "_output", "net8.0")
        ];

        foreach (string? candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "Lidarr.dll")))
            {
                return candidate;
            }
        }

        return null;
    }
}

