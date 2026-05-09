using System.Threading.Tasks;
using Xunit;

namespace Tidalarr.Tests.Runtime;

/// <summary>
/// Original (wave-12) Docker smoke test, retained for backwards compatibility
/// with anyone running <c>dotnet test --filter "Category=Docker"</c>.
///
/// As of wave 21 the actual orchestration lives in
/// <see cref="LidarrContainerFixture"/> and the expanded smoke matrix lives in
/// <see cref="DockerE2ETests"/>. This test simply re-asserts the original
/// "plugin appears in indexer schema" claim against the shared container so we
/// do not pay the container-startup cost twice.
///
/// Run: dotnet test --filter "Category=Docker" or "Category=DockerE2E"
/// </summary>
[Collection(LidarrContainerCollection.Name)]
public sealed class DockerSmokeTests
{
    private readonly LidarrContainerFixture _fixture;

    public DockerSmokeTests(LidarrContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "DockerE2E")]
    public async Task Plugin_Loads_In_Real_Lidarr_Container()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);

        string url = $"{_fixture.BaseUrl}/api/v1/indexer/schema?apikey={_fixture.ApiKey}";
        string schemaJson = await _fixture.Http.GetStringAsync(url);

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(schemaJson);
        bool hasTidalIndexer = false;
        foreach (System.Text.Json.JsonElement entry in doc.RootElement.EnumerateArray())
        {
            string name = entry.TryGetProperty("name", out System.Text.Json.JsonElement n) ? n.GetString() ?? "" : "";
            string impl = entry.TryGetProperty("implementation", out System.Text.Json.JsonElement i) ? i.GetString() ?? "" : "";
            if (name.Contains("Tidal", System.StringComparison.OrdinalIgnoreCase)
                || impl.Contains("Tidal", System.StringComparison.OrdinalIgnoreCase))
            {
                hasTidalIndexer = true;
                break;
            }
        }

        Assert.True(hasTidalIndexer,
            $"Expected indexer schema to contain a Tidal indexer. Schema response: {Truncate(schemaJson, 2000)}");
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "... (truncated)";
}
