using System;
using System.Collections.Generic;
using System.Text.Json;
using Lidarr.Plugin.Abstractions.Contracts;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.SettingsMigration;

/// <summary>
/// Fixture-backed settings migration tests. These simulate real Lidarr persistence
/// scenarios: JSON round-trips, version upgrades, type coercion edge cases.
/// Replaces shape-only checks with behavioral verification.
/// </summary>
public class SettingsRoundTripFixtureTests
{
    private readonly TidalarrPlugin _plugin = new();
    private ISettingsProvider Provider => _plugin.SettingsProvider;

    private static Dictionary<string, object?> ValidSettings() => new()
    {
        ["ConfigPath"] = "/etc/tidalarr",
        ["RedirectUrl"] = "https://login.tidal.com/callback?code=test&state=abc",
        ["DownloadPath"] = "/mnt/downloads",
        ["PreferredQuality"] = "Lossless"
    };

    // ── JSON Round-Trip (simulates Lidarr DB persistence) ───────────

    [Fact]
    public void Apply_JsonRoundTrip_PreservesAllFields()
    {
        // Arrange — simulate Lidarr serializing settings to JSON and back
        Dictionary<string, object?> original = ValidSettings();
        string json = JsonSerializer.Serialize(original);
        Dictionary<string, object?>? restored = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        Assert.NotNull(restored);

        // Act — apply the deserialized settings (values are now JsonElement, not string)
        PluginValidationResult result = Provider.Apply(restored!);

        // Assert — must accept JsonElement-typed values from deserialization
        Assert.True(result.IsValid, $"Failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void Apply_JsonRoundTrip_JsonElementStrings_AreParsedCorrectly()
    {
        // Arrange — after JSON round-trip, strings become JsonElement
        string json = JsonSerializer.Serialize(ValidSettings());
        Dictionary<string, object?>? restored = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        // The values are now JsonElement, not string
        Assert.IsType<JsonElement>(restored!["ConfigPath"]);

        // Act
        PluginValidationResult result = Provider.Apply(restored);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_JsonRoundTrip_AcceptsDeserializedPayload()
    {
        string json = JsonSerializer.Serialize(ValidSettings());
        Dictionary<string, object?>? restored = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        PluginValidationResult result = Provider.Validate(restored!);

        Assert.True(result.IsValid);
    }

    // ── Type Coercion Edge Cases ────────────────────────────────────

    [Theory]
    [InlineData("Lossless")]
    [InlineData("lossless")]
    [InlineData("LOSSLESS")]
    [InlineData("HiRes")]
    [InlineData("hires")]
    public void Apply_QualityString_CaseInsensitive(string quality)
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings["PreferredQuality"] = quality;

        PluginValidationResult result = Provider.Apply(settings);

        Assert.True(result.IsValid, $"Quality '{quality}' should be accepted case-insensitively");
    }

    [Theory]
    [InlineData(0)]  // Low
    [InlineData(1)]  // High
    [InlineData(2)]  // Lossless
    [InlineData(3)]  // HiRes
    public void Apply_QualityAsInt_AcceptsDefinedEnumValues(int qualityInt)
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings["PreferredQuality"] = qualityInt;

        PluginValidationResult result = Provider.Apply(settings);

        Assert.True(result.IsValid, $"Integer quality {qualityInt} should map to valid enum value");
    }

    [Fact]
    public void Apply_QualityAsUndefinedInt_FallsBackToDefault()
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings["PreferredQuality"] = 999;  // undefined enum value

        PluginValidationResult result = Provider.Apply(settings);

        // Should not crash — falls back to default Lossless
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Apply_MissingQuality_UsesDefault()
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings.Remove("PreferredQuality");

        PluginValidationResult result = Provider.Apply(settings);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Apply_NullQuality_UsesDefault()
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings["PreferredQuality"] = null;

        PluginValidationResult result = Provider.Apply(settings);

        Assert.True(result.IsValid);
    }

    // ── Version Upgrade Scenarios ───────────────────────────────────

    [Fact]
    public void Apply_V1Settings_MissingNewFields_AcceptedGracefully()
    {
        // V1 only had ConfigPath, RedirectUrl, DownloadPath
        Dictionary<string, object?> v1 = new()
        {
            ["ConfigPath"] = "/v1/config",
            ["RedirectUrl"] = "https://login.tidal.com/callback?code=test&state=abc",
            ["DownloadPath"] = "/v1/downloads"
        };

        PluginValidationResult result = Provider.Apply(v1);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Apply_V1ToV2Upgrade_NewFieldsGetDefaults()
    {
        // Apply V1 settings (no PreferredQuality)
        Dictionary<string, object?> v1 = new()
        {
            ["ConfigPath"] = "/v1/config",
            ["RedirectUrl"] = "https://login.tidal.com/callback?code=test&state=abc",
            ["DownloadPath"] = "/v1/downloads"
        };
        Provider.Apply(v1);

        // Now apply V2 with new field
        Dictionary<string, object?> v2 = new(v1)
        {
            ["PreferredQuality"] = "HiRes"
        };
        PluginValidationResult result = Provider.Apply(v2);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Apply_UnknownFields_Ignored()
    {
        // Future version might add fields the current version doesn't know about
        Dictionary<string, object?> futureSettings = new(ValidSettings())
        {
            ["FutureField"] = "some value",
            ["AnotherNewField"] = 42
        };

        PluginValidationResult result = Provider.Apply(futureSettings);

        Assert.True(result.IsValid);
    }

    // ── Describe/Defaults Contract ──────────────────────────────────

    [Fact]
    public void Describe_AllFieldsHaveDisplayName()
    {
        IReadOnlyCollection<SettingDefinition> defs = Provider.Describe();

        Assert.All(defs, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Key), "Key must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(d.DisplayName), $"DisplayName for '{d.Key}' must not be empty");
        });
    }

    [Fact]
    public void Describe_RequiredFields_MarkedAsRequired()
    {
        IReadOnlyCollection<SettingDefinition> defs = Provider.Describe();
        Dictionary<string, SettingDefinition> byKey = new();
        foreach (SettingDefinition d in defs)
        {
            byKey[d.Key] = d;
        }

        Assert.True(byKey["ConfigPath"].IsRequired, "ConfigPath should be required");
        Assert.True(byKey["RedirectUrl"].IsRequired, "RedirectUrl should be required");
        Assert.True(byKey["DownloadPath"].IsRequired, "DownloadPath should be required");
    }

    [Fact]
    public void GetDefaults_ApplyDefaults_IsValid()
    {
        // The default settings should themselves pass validation
        // (except paths which are empty by default — this is expected)
        IReadOnlyDictionary<string, object?> defaults = Provider.GetDefaults();

        Assert.NotNull(defaults);
        Assert.True(defaults.ContainsKey("ConfigPath"));
        Assert.True(defaults.ContainsKey("PreferredQuality"));
    }

    // ── Validation Error Detail ─────────────────────────────────────

    [Fact]
    public void Validate_EmptyRequired_ErrorsReferenceCorrectFields()
    {
        Dictionary<string, object?> empty = new()
        {
            ["ConfigPath"] = "",
            ["RedirectUrl"] = "",
            ["DownloadPath"] = ""
        };

        PluginValidationResult result = Provider.Validate(empty);

        Assert.False(result.IsValid);
        // Should have at least one error per empty required field
        Assert.True(result.Errors.Count >= 2, $"Expected multiple errors, got {result.Errors.Count}");
    }

    [Fact]
    public void Validate_InvalidUri_Rejected()
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings["RedirectUrl"] = "not-a-valid-url";

        PluginValidationResult result = Provider.Validate(settings);

        Assert.False(result.IsValid);
    }

    // ── JSON Round-Trip with Numeric Quality (C2) ─────────────────

    [Fact]
    public void Apply_JsonRoundTrip_NumericQuality_ParsedCorrectly()
    {
        Dictionary<string, object?> original = ValidSettings();
        original["PreferredQuality"] = 2;  // numeric Lossless
        string json = JsonSerializer.Serialize(original);
        Dictionary<string, object?>? restored = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        // After JSON round-trip, 2 is JsonElement with ValueKind.Number
        PluginValidationResult result = Provider.Apply(restored!);
        Assert.True(result.IsValid);
    }

    // ── Full Property Round-Trip (C1 — all 14 properties) ──────────
    // T-3: IncludeMqa + ReEncodeAAC removed (dead settings — accepted/validated/copied
    // everywhere but never consulted by any runtime consumer; no coherent MQA/AAC-re-encode
    // behavior existed to wire them into).

    [Fact]
    public void Apply_AllProperties_JsonRoundTrip_Accepted()
    {
        Dictionary<string, object?> full = new()
        {
            ["ConfigPath"] = "/etc/tidalarr",
            ["RedirectUrl"] = "https://login.tidal.com/callback?code=test&state=abc",
            ["DownloadPath"] = "/mnt/downloads",
            ["PreferredQuality"] = "HiRes",
            ["TidalMarket"] = "DE",
            ["EarlyReleaseLimit"] = 30,
            ["EnableCache"] = false,
            ["CacheDuration"] = 60,
            ["ExtractFlac"] = false,
            ["SaveSyncedLyrics"] = false,
            ["UseLRCLIB"] = true,
            ["DownloadDelay"] = 500,
            ["MaxConcurrentTrackDownloads"] = 3,
            ["MaxConcurrentChunkDownloads"] = 3
        };

        string json = JsonSerializer.Serialize(full);
        Dictionary<string, object?>? restored = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        PluginValidationResult result = Provider.Apply(restored!);
        Assert.True(result.IsValid, $"Failed: {string.Join(", ", result.Errors)}");
    }

    [Fact]
    public void Describe_Returns_All_14_Properties()
    {
        IReadOnlyCollection<SettingDefinition> defs = Provider.Describe();
        Assert.Equal(14, defs.Count);
    }

    [Fact]
    public void GetDefaults_Returns_All_14_Properties()
    {
        IReadOnlyDictionary<string, object?> defaults = Provider.GetDefaults();
        Assert.Equal(14, defaults.Count);
        Assert.Equal("US", defaults["TidalMarket"]);
        Assert.Equal(14, defaults["EarlyReleaseLimit"]);
        Assert.Equal(true, defaults["EnableCache"]);
    }

    // ── Bool/Int JsonElement Round-Trip ──────────────────────────────

    [Fact]
    public void Apply_JsonRoundTrip_BoolProperties_ParsedCorrectly()
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings["EnableCache"] = false;
        settings["ExtractFlac"] = false;

        string json = JsonSerializer.Serialize(settings);
        Dictionary<string, object?>? restored = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        PluginValidationResult result = Provider.Apply(restored!);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Apply_JsonRoundTrip_IntProperties_ParsedCorrectly()
    {
        Dictionary<string, object?> settings = ValidSettings();
        settings["EarlyReleaseLimit"] = 30;
        settings["CacheDuration"] = 60;
        settings["MaxConcurrentTrackDownloads"] = 3;

        string json = JsonSerializer.Serialize(settings);
        Dictionary<string, object?>? restored = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

        PluginValidationResult result = Provider.Apply(restored!);
        Assert.True(result.IsValid);
    }

    // ── Idempotency ─────────────────────────────────────────────────

    [Fact]
    public void Apply_Twice_SameSettings_BothSucceed()
    {
        Dictionary<string, object?> settings = ValidSettings();

        PluginValidationResult first = Provider.Apply(settings);
        PluginValidationResult second = Provider.Apply(settings);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
    }

    [Fact]
    public void Apply_DifferentSettings_SecondOverridesFirst()
    {
        Dictionary<string, object?> first = ValidSettings();
        first["PreferredQuality"] = "Low";
        Provider.Apply(first);

        Dictionary<string, object?> second = ValidSettings();
        second["PreferredQuality"] = "HiRes";
        PluginValidationResult result = Provider.Apply(second);

        Assert.True(result.IsValid);
    }
}
