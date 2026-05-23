using System.Threading.Tasks;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Xunit;

namespace Tidalarr.Tests.ReleaseE2E;

/// <summary>
/// Regression backstop against Lidarr's <c>PluginService.GetRemotePlugin</c> filter
/// applied to the LIVE GitHub releases of this repo. Actual assertions live in
/// <see cref="PublishedReleaseInstallabilityContract"/> in Common.TestKit.
///
/// <para>Opt-in via [Trait("Category", "ReleaseE2E")]; skipped in the default sweep.</para>
/// </summary>
public class PublishedReleaseInstallabilityTests
{
    private const string Owner = "RicherTunes";
    private const string Repo = "Tidalarr";

    private static readonly PluginPackagePolicy Policy = PluginPackagingContract.MergedDllPolicy(
        mainAssemblyName: "Lidarr.Plugin.Tidalarr");

    [SkippableFact]
    [Trait("Category", "ReleaseE2E")]
    public Task LatestPublishedRelease_PassesLidarrInstallFilter() =>
        PublishedReleaseInstallabilityContract
            .AssertLatestReleasePassesLidarrInstallFilterAsync(Owner, Repo);

    [SkippableFact]
    [Trait("Category", "ReleaseE2E")]
    public Task LatestPublishedRelease_ZipContents_Match_PackagingPolicy() =>
        PublishedReleaseInstallabilityContract
            .AssertLatestReleaseZipMatchesPolicyAsync(Owner, Repo, Policy);
}
