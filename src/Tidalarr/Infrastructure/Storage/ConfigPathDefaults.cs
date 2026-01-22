using System;
using System.IO;

namespace Tidalarr.Infrastructure.Storage;

internal static class ConfigPathDefaults
{
    internal const string DefaultDockerConfigRoot = "/config";

    internal static string GetDefaultConfigPath(
        string appName,
        string? dockerConfigRootOverride = null,
        string? applicationDataOverride = null,
        string? homeOverride = null)
    {
        string dockerRoot = dockerConfigRootOverride ?? DefaultDockerConfigRoot;

        // Docker environment: /config is the standard writable mount point (hotio/linuxserver).
        // Prefer it when present to avoid $HOME pointing at /root for non-root container users.
        if (Directory.Exists(dockerRoot))
        {
            return Path.Combine(dockerRoot, appName);
        }

        // Windows: %AppData%. Linux/macOS: ~/.config
        string applicationData = applicationDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(applicationData))
        {
            return Path.Combine(applicationData, appName);
        }

        // Fall back to HOME/.config on Linux/macOS
        string? home = homeOverride ?? Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
        {
            return Path.Combine(home, ".config", appName);
        }

        // Last resort: use /config/<appName>
        return Path.Combine(DefaultDockerConfigRoot, appName);
    }
}

