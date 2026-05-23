using System.IO;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Tests.Utils;
using Xunit;

namespace Tidalarr.Tests.Unit.Packaging;

/// <summary>
/// Tidalarr package compliance. The four cross-plugin assertions (required files,
/// forbidden DLLs, merged-DLL size, no host leaks) delegate to the shared
/// <see cref="PluginPackagingContract"/> in Common.TestKit so the rules don't
/// drift across the four-plugin family. Tidalarr-specific extras (reasonable size
/// envelope, plugin.json Main field cross-check) stay inline.
///
/// The previous standalone <c>PackagingPolicyBaseline.cs</c> (which loaded the
/// required/forbidden lists from <c>docs/PACKAGING_POLICY_BASELINE.md</c>) is
/// superseded — Common.TestKit's <see cref="PluginPackagingContract.MergedDllPolicy"/>
/// is now the source of truth. The doc and <c>packaging/expected-contents.txt</c>
/// can stay as human-readable references but are no longer test-driven.
/// </summary>
public sealed class PluginPackagingPolicyTests
{
    private static readonly PluginPackagePolicy Policy = PluginPackagingContract.MergedDllPolicy(
        mainAssemblyName: "Lidarr.Plugin.Tidalarr");

    [PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Matches_Cross_Plugin_Policy()
    {
        string packagePath = PackagingTestPaths.RequirePackagePath();
        PluginPackagingContract.AssertZipMatchesPolicy(packagePath, Policy);
    }

    // ----- Tidalarr-specific extras (not duplicated across plugins) -----

    [PackagingFact]
    [Trait("Category", "Packaging")]
    public void Package_Should_Have_Reasonable_Size()
    {
        string packagePath = PackagingTestPaths.RequirePackagePath();
        long sizeBytes = new FileInfo(packagePath).Length;

        Assert.True(sizeBytes > 100_000,
            "a plugin package smaller than 100KB likely indicates a packaging failure");
        Assert.True(sizeBytes < 15 * 1024 * 1024,
            "package bloat usually indicates an accidental dependency leak");
    }
}
