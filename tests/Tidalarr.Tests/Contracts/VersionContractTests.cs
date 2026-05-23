using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Contracts;

/// <summary>
/// Catches version drift between TidalModule.Version (assembly-derived), plugin.json,
/// and the top-level VERSION file. Actual assertions live in
/// <see cref="PluginVersionContract"/> in Common.TestKit. Tidalarr does not ship a
/// separate manifest.json so the AssertManifestMatchesPluginJson sibling is omitted.
///
/// Replaces the ~78 LOC of inlined LocatePluginJson / LocateRepoFile / JsonDocument
/// reading with delegated calls.
/// </summary>
public class VersionContractTests
{
    [Fact]
    public void TidalModuleVersion_MatchesPluginJsonVersion() =>
        PluginVersionContract.AssertAssemblyVersionMatchesPluginJson(typeof(TidalarrInstalledPlugin));

    [Fact]
    public void VersionFile_MatchesPluginJsonVersion() =>
        PluginVersionContract.AssertVersionFileMatchesPluginJson(typeof(TidalarrInstalledPlugin));
}
