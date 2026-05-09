using System.Threading.Tasks;
using Lidarr.Plugin.Common.TestKit.Hosting;
using Xunit;

namespace Tidalarr.Tests.Runtime;

/// <summary>
/// End-to-end smoke tests that exercise the tidalarr plugin inside a real
/// Lidarr container. Boots once via <see cref="TidalarrLidarrContainerFixture"/>
/// (which subclasses common's lifted
/// <see cref="Lidarr.Plugin.Common.TestKit.Hosting.LidarrContainerFixture"/>),
/// then runs four assertions to verify the plugin is actually wired into the
/// host (not merely loadable in a sandbox):
///
///  1. Indexer schema lists a Tidal indexer
///  2. DownloadClient schema lists a Tidal download client
///  3. POST /api/v1/indexer/test with empty settings returns a sensible 4xx
///     (validation failure), not a 500 (plugin-internal error)
///  4. POST /api/v1/downloadclient/test with empty settings returns a sensible 4xx
///
/// All tests are gated on <c>[Trait("Category","DockerE2E")]</c> and skip
/// gracefully when Docker isn't running or the plugin DLL isn't built.
///
/// Wave 22a — the orchestration + assertion logic now lives in common's
/// TestKit (LidarrContainerFixture + LidarrContainerFixtureSmokeAssertions).
/// This file is just per-plugin glue.
/// </summary>
[Collection(LidarrContainerCollection.Name)]
public sealed class DockerE2ETests
{
    private readonly TidalarrLidarrContainerFixture _fixture;

    public DockerE2ETests(TidalarrLidarrContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task Plugin_Loads_AppearsInIndexerSchema()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await _fixture.AssertPluginAppearsInIndexerSchemaAsync();
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task Plugin_Loads_AppearsInDownloadClientSchema()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await _fixture.AssertPluginAppearsInDownloadClientSchemaAsync();
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task Indexer_Test_WithEmptySettings_ReturnsSensibleFailure()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await _fixture.AssertIndexerTestReturnsSensibleFailureAsync();
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task DownloadClient_Test_WithEmptySettings_ReturnsSensibleFailure()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await _fixture.AssertDownloadClientTestReturnsSensibleFailureAsync();
    }
}
