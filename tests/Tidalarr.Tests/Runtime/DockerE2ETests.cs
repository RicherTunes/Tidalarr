using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Tidalarr.Tests.Runtime;

/// <summary>
/// End-to-end smoke tests that exercise the tidalarr plugin inside a real
/// Lidarr container. Boots once via <see cref="LidarrContainerFixture"/>,
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
/// Wave 21 — see CLAUDE.md "Docker E2E Harness" section for how to run these
/// locally and how to extend them to other plugins (wave 22).
/// </summary>
[Collection(LidarrContainerCollection.Name)]
public sealed class DockerE2ETests
{
    private readonly LidarrContainerFixture _fixture;

    public DockerE2ETests(LidarrContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task Plugin_Loads_AppearsInIndexerSchema()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);

        string url = $"{_fixture.BaseUrl}/api/v1/indexer/schema?apikey={_fixture.ApiKey}";
        string json = await _fixture.Http.GetStringAsync(url);

        Assert.True(SchemaContainsTidal(json),
            $"Expected indexer schema to include a Tidal entry. Logs:\n{Truncate(_fixture.GetContainerLogs(), 2000)}\n\nSchema:\n{Truncate(json, 1500)}");
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task Plugin_Loads_AppearsInDownloadClientSchema()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);

        string url = $"{_fixture.BaseUrl}/api/v1/downloadclient/schema?apikey={_fixture.ApiKey}";
        string json = await _fixture.Http.GetStringAsync(url);

        Assert.True(SchemaContainsTidal(json),
            $"Expected downloadclient schema to include a Tidal entry. Logs:\n{Truncate(_fixture.GetContainerLogs(), 2000)}\n\nSchema:\n{Truncate(json, 1500)}");
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task Indexer_Test_WithEmptySettings_ReturnsSensibleFailure()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);

        // Pull the Tidal indexer schema entry and POST it back to /test.
        // Lidarr's Test endpoint expects a full indexer definition shape; using the schema
        // (which has no user-supplied credentials) should produce a settings-validation
        // failure (4xx) — NOT a 500 internal error caused by a plugin load fault.
        JsonElement? tidalSchema = await GetTidalSchemaEntryAsync("indexer");
        Skip.If(tidalSchema is null, "No Tidal indexer schema entry returned (plugin likely not loaded — see other test for diagnosis).");

        await AssertTestEndpointReturnsValidationFailureAsync("indexer", tidalSchema!.Value);
    }

    [SkippableFact]
    [Trait("Category", "DockerE2E")]
    public async Task DownloadClient_Test_WithEmptySettings_ReturnsSensibleFailure()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);

        JsonElement? tidalSchema = await GetTidalSchemaEntryAsync("downloadclient");
        Skip.If(tidalSchema is null, "No Tidal downloadclient schema entry returned (plugin likely not loaded — see other test for diagnosis).");

        await AssertTestEndpointReturnsValidationFailureAsync("downloadclient", tidalSchema!.Value);
    }

    // -- helpers ---------------------------------------------------------

    private async Task<JsonElement?> GetTidalSchemaEntryAsync(string kind)
    {
        string schemaUrl = $"{_fixture.BaseUrl}/api/v1/{kind}/schema?apikey={_fixture.ApiKey}";
        string json = await _fixture.Http.GetStringAsync(schemaUrl);
        using JsonDocument doc = JsonDocument.Parse(json);

        foreach (JsonElement entry in doc.RootElement.EnumerateArray())
        {
            if (EntryReferencesTidal(entry))
            {
                // Clone so the JsonDocument can be disposed
                return JsonDocument.Parse(entry.GetRawText()).RootElement;
            }
        }

        return null;
    }

    private async Task AssertTestEndpointReturnsValidationFailureAsync(string kind, JsonElement schemaEntry)
    {
        string testUrl = $"{_fixture.BaseUrl}/api/v1/{kind}/test?apikey={_fixture.ApiKey}";

        // Lidarr's Test endpoint accepts the same shape as the resource (schema-derived
        // entry already includes `name`, `implementation`, `configContract`, and `fields`
        // with default values). With no real credentials, the plugin should report a
        // validation failure (typically 400 Bad Request with an array of validation errors).
        using StringContent content = new(schemaEntry.GetRawText(), System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _fixture.Http.PostAsync(testUrl, content);
        string body = await response.Content.ReadAsStringAsync();

        // The acceptance criterion: NOT 500. A real plugin-load failure (missing types,
        // bad assemblies, etc.) shows up as 500 InternalServerError. Validation failures
        // are 400 BadRequest. Anything in 2xx-4xx is acceptable; 5xx is the smoke alarm.
        Assert.True(
            (int)response.StatusCode < 500,
            $"Expected non-5xx from /{kind}/test (plugin load smoke), got {(int)response.StatusCode} {response.StatusCode}.\n" +
            $"Body: {Truncate(body, 1500)}\n" +
            $"Logs:\n{Truncate(_fixture.GetContainerLogs(), 1500)}");
    }

    private static bool SchemaContainsTidal(string schemaJson)
    {
        using JsonDocument doc = JsonDocument.Parse(schemaJson);
        return doc.RootElement.EnumerateArray().Any(EntryReferencesTidal);
    }

    private static bool EntryReferencesTidal(JsonElement entry)
    {
        string name = entry.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "" : "";
        string impl = entry.TryGetProperty("implementation", out JsonElement i) ? i.GetString() ?? "" : "";
        return name.Contains("Tidal", StringComparison.OrdinalIgnoreCase)
            || impl.Contains("Tidal", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "... (truncated)";
}
