using Tidalarr.Infrastructure.Storage;

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

    // --- Wave 2: expanded coverage ---

    [Fact]
    [Trait("Category", "Wave2")]
    public void GetDefaultConfigPath_ReturnsNonEmptyString_ContainsPluginName()
    {
        // Even with no overrides, the method must return something that includes the app name.
        // We force all fallbacks to empty/nonexistent so it hits the last-resort branch.
        string nonExistentDocker = Path.Combine(Path.GetTempPath(), $"tidalarr-no-exist-{Guid.NewGuid():N}");

        string result = ConfigPathDefaults.GetDefaultConfigPath(
            appName: "Tidalarr",
            dockerConfigRootOverride: nonExistentDocker,
            applicationDataOverride: "",
            homeOverride: "");

        Assert.False(string.IsNullOrWhiteSpace(result), "Config path must not be null or whitespace");
        Assert.Contains("Tidalarr", result, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Wave2")]
    public void GetDefaultConfigPath_WhenAppDataEmpty_FallsBackToHome()
    {
        string nonExistentDocker = Path.Combine(Path.GetTempPath(), $"tidalarr-no-docker-{Guid.NewGuid():N}");
        string home = Path.Combine(Path.GetTempPath(), $"tidalarr-home-{Guid.NewGuid():N}");

        string result = ConfigPathDefaults.GetDefaultConfigPath(
            appName: "Tidalarr",
            dockerConfigRootOverride: nonExistentDocker,
            applicationDataOverride: "",
            homeOverride: home);

        Assert.Equal(Path.Combine(home, ".config", "Tidalarr"), result);
    }

    [Fact]
    [Trait("Category", "Wave2")]
    public void GetDefaultConfigPath_WhenAllOverridesEmpty_FallsBackToDefaultDockerRoot()
    {
        string nonExistentDocker = Path.Combine(Path.GetTempPath(), $"tidalarr-no-docker-{Guid.NewGuid():N}");

        string result = ConfigPathDefaults.GetDefaultConfigPath(
            appName: "Tidalarr",
            dockerConfigRootOverride: nonExistentDocker,
            applicationDataOverride: "",
            homeOverride: "");

        // Last resort: /config/<appName>
        Assert.Equal(Path.Combine(ConfigPathDefaults.DefaultDockerConfigRoot, "Tidalarr"), result);
    }

    [Fact]
    [Trait("Category", "Wave2")]
    public void GetDefaultConfigPath_DefaultDockerConfigRoot_IsSlashConfig()
    {
        Assert.Equal("/config", ConfigPathDefaults.DefaultDockerConfigRoot);
    }

    [Theory]
    [Trait("Category", "Wave2")]
    [InlineData("MyPlugin")]
    [InlineData("Qobuzarr")]
    [InlineData("AppleMusicarr")]
    public void GetDefaultConfigPath_DifferentAppNames_AppearInResult(string appName)
    {
        string nonExistentDocker = Path.Combine(Path.GetTempPath(), $"tidalarr-no-docker-{Guid.NewGuid():N}");

        string result = ConfigPathDefaults.GetDefaultConfigPath(
            appName: appName,
            dockerConfigRootOverride: nonExistentDocker,
            applicationDataOverride: "",
            homeOverride: "");

        Assert.EndsWith(appName, result);
    }
}

