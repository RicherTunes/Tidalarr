using System;
using System.IO;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class TidalLidarrDownloadClientPathSanitizationTests
{
    [Fact]
    public void BuildOutputPath_Uses_FileSystemUtilities_SanitizeFileName()
    {
        var repoRoot = GetRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "src", "Tidalarr", "Integration", "LidarrNative", "TidalLidarrDownloadClient.cs");

        Assert.True(File.Exists(sourcePath), $"Expected source file at {sourcePath}");

        var content = File.ReadAllText(sourcePath);
        Assert.Contains("FileSystemUtilities.SanitizeFileName(", content, StringComparison.Ordinal);
    }

    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Tidalarr.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? throw new DirectoryNotFoundException("Failed to find repo root.");
        }

        throw new DirectoryNotFoundException("Could not locate repo root containing Tidalarr.sln");
    }
}
