using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Tidalarr.Tests.Documentation;

public class DocumentationTruthTests
{
    [Fact]
    public void TidalDownloadClientSource_DoesNotClaimTidalDownloadItemWasRemoved()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "Tidalarr", "Integration", "LidarrNative", "TidalLidarrDownloadClient.cs"));

        source.Should().NotContain("TidalDownloadItem removed",
            "TidalDownloadItem is the active plugin-local tracker subclass used to surface failure messages safely");
    }

    [Fact]
    public void Docs_AdvertiseIsrcTagWritingWhenCommonMetadataApplierIsActive()
    {
        var root = FindRepositoryRoot();
        var moduleSource = File.ReadAllText(Path.Combine(root, "src", "Tidalarr", "Integration", "TidalModule.cs"));
        var orchestratorSource = File.ReadAllText(Path.Combine(root, "ext", "Lidarr.Plugin.Common", "src", "Services", "Download", "SimpleDownloadOrchestrator.cs"));
        var applierSource = File.ReadAllText(Path.Combine(root, "ext", "Lidarr.Plugin.Common", "src", "Services", "Metadata", "TagLibAudioMetadataApplier.cs"));
        var mapperSource = File.ReadAllText(Path.Combine(root, "src", "Tidalarr", "Core", "Mappers", "TidalModelMapper.cs"));

        moduleSource.Should().Contain("metadataApplier: null");
        orchestratorSource.Should().Contain("_metadataApplier = metadataApplier ?? new TagLibAudioMetadataApplier()");
        orchestratorSource.Should().Contain("await _metadataApplier.ApplyAsync");
        applierSource.Should().Contain("ApplyIsrc(file, normalizedIsrc)");
        mapperSource.Should().Contain("Isrc = track.Isrc");

        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var qualityWiki = File.ReadAllText(Path.Combine(root, "wiki", "Quality-and-Formats.md"));
        var homeWiki = File.ReadAllText(Path.Combine(root, "wiki", "Home.md"));

        readme.Should().Contain("ISRC tag writing",
            "the Common download orchestrator defaults to TagLibAudioMetadataApplier when Tidal passes metadataApplier: null");
        qualityWiki.Should().Contain("ISRC codes captured from the Tidal API are written",
            "Tidal maps track ISRC values into StreamingTrack and Common writes them when present");
        qualityWiki.Should().Contain("ISRC tags help Lidarr match imports",
            "documentation should explain why the metadata is preserved");
        homeWiki.Should().Contain("ISRC tags",
            "wiki summaries should advertise active metadata behavior");
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "Tidalarr.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException($"Could not locate repo root from {AppContext.BaseDirectory}");
    }
}
