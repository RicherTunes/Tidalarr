using System;
using System.IO;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class ConfigPathDefaultsTests
{
    [Fact]
    public void GetDefaultConfigPath_WhenDockerRootExists_PrioritizesDockerConfig()
    {
        string dockerRoot = Path.Combine(Path.GetTempPath(), $"tidalarr-configroot-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(dockerRoot);

            string result = ConfigPathDefaults.GetDefaultConfigPath(
                appName: "Tidalarr",
                dockerConfigRootOverride: dockerRoot,
                applicationDataOverride: "C:\\should-not-be-used",
                homeOverride: "/should-not-be-used");

            Assert.Equal(Path.Combine(dockerRoot, "Tidalarr"), result);
        }
        finally
        {
            try { Directory.Delete(dockerRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public void GetDefaultConfigPath_WhenDockerRootMissing_FallsBackToApplicationData()
    {
        string dockerRoot = Path.Combine(Path.GetTempPath(), $"tidalarr-missing-configroot-{Guid.NewGuid():N}");
        string applicationData = Path.Combine(Path.GetTempPath(), $"tidalarr-appdata-{Guid.NewGuid():N}");
        try
        {
            string result = ConfigPathDefaults.GetDefaultConfigPath(
                appName: "Tidalarr",
                dockerConfigRootOverride: dockerRoot,
                applicationDataOverride: applicationData,
                homeOverride: "/should-not-be-used");

            Assert.Equal(Path.Combine(applicationData, "Tidalarr"), result);
        }
        finally
        {
            try { Directory.Delete(applicationData, recursive: true); } catch { }
        }
    }
}

