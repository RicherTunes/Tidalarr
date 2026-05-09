using System.Threading.Tasks;
using Lidarr.Plugin.Common.TestKit.Hosting;
using Xunit;

namespace Tidalarr.Tests.Runtime;

/// <summary>
/// Original (wave-12) Docker smoke test, retained for backwards compatibility
/// with anyone running <c>dotnet test --filter "Category=Docker"</c>.
///
/// As of wave 22a the orchestration + assertion logic lives in common's
/// TestKit (LidarrContainerFixture + LidarrContainerFixtureSmokeAssertions).
/// This test simply re-asserts the original "plugin appears in indexer schema"
/// claim against the shared container so we do not pay the container-startup
/// cost twice.
///
/// Run: dotnet test --filter "Category=Docker" or "Category=DockerE2E"
/// </summary>
[Collection(LidarrContainerCollection.Name)]
public sealed class DockerSmokeTests
{
    private readonly TidalarrLidarrContainerFixture _fixture;

    public DockerSmokeTests(TidalarrLidarrContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "DockerE2E")]
    public async Task Plugin_Loads_In_Real_Lidarr_Container()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await _fixture.AssertPluginAppearsInIndexerSchemaAsync();
    }
}
