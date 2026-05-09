using System.IO;
using System.Linq;
using Lidarr.Plugin.Common.TestKit.Hosting;
using Xunit;

namespace Tidalarr.Tests.Runtime;

/// <summary>
/// Tidalarr-specific subclass that pre-fills the per-plugin
/// <see cref="LidarrContainerOptions"/> consumed by common's lifted
/// <see cref="Lidarr.Plugin.Common.TestKit.Hosting.LidarrContainerFixture"/>.
///
/// Wave 22a — the orchestration logic (container lifecycle, healthcheck,
/// log capture, skip-when-no-Docker) was lifted into TestKit. This file
/// keeps only the per-plugin constants:
///   - container name           : tidalarr-e2e
///   - host port                : 8690 (single-plugin instance per CLAUDE.md)
///   - Docker image             : pinned net8 plugins-branch tag
///   - plugin mount path        : /config/plugins/RicherTunes/Tidalarr
///   - plugin DLL filename      : Lidarr.Plugin.Tidalarr.dll
///   - schema-entry substring   : "Tidal"
///   - plugin DLL discovery     : src/Tidalarr/bin/{Release|Debug}/Lidarr.Plugin.Tidalarr.dll
/// </summary>
public sealed class TidalarrLidarrContainerFixture
    : Lidarr.Plugin.Common.TestKit.Hosting.LidarrContainerFixture
{
    public TidalarrLidarrContainerFixture()
        : base(BuildOptions())
    {
    }

    private static LidarrContainerOptions BuildOptions() => new(
        DockerImage: "ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913",
        ContainerName: "tidalarr-e2e",
        LidarrPort: 8690,
        PluginMountPath: "/config/plugins/RicherTunes/Tidalarr",
        PluginDllFileName: "Lidarr.Plugin.Tidalarr.dll",
        FindPluginDll: FindTidalarrPluginDll,
        PluginEntrySubstring: "Tidal",
        RepoRootMarkerFile: "Tidalarr.sln");

    private static string? FindTidalarrPluginDll(string repoRoot)
    {
        string[] candidates =
        [
            Path.Combine(repoRoot, "src", "Tidalarr", "bin", "Lidarr.Plugin.Tidalarr.dll"),
            Path.Combine(repoRoot, "src", "Tidalarr", "bin", "Release", "Lidarr.Plugin.Tidalarr.dll"),
            Path.Combine(repoRoot, "src", "Tidalarr", "bin", "Debug", "Lidarr.Plugin.Tidalarr.dll"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }
}

/// <summary>
/// xUnit collection definition that lets all E2E tests share the single
/// <see cref="TidalarrLidarrContainerFixture"/> instance.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LidarrContainerCollection : ICollectionFixture<TidalarrLidarrContainerFixture>
{
    public const string Name = "LidarrContainer";
}
