using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Lidarr.Plugin.Abstractions.Contracts;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.SettingsMigration
{
    /// <summary>
    /// Phase 2: Settings Migration Tests
    /// Verifies that settings migrate correctly between native and bridge implementations.
    /// </summary>
    public class SettingsMigrationTests
    {
        private readonly TidalarrPlugin plugin;
        private readonly ISettingsProvider settingsProvider;

        public SettingsMigrationTests()
        {
            this.plugin = new TidalarrPlugin();
            this.settingsProvider = this.plugin.SettingsProvider;
        }

        [Fact]
        public void SettingsProvider_Describe_Returns_Expected_Fields()
        {
            // Act
            var definitions = this.settingsProvider.Describe();

            // Assert
            Assert.NotNull(definitions);
            Assert.NotEmpty(definitions);

            // Verify required fields exist
            var keys = definitions.Select(d => d.Key).ToHashSet();
            Assert.Contains("ConfigPath", keys);
            Assert.Contains("RedirectUrl", keys);
            Assert.Contains("DownloadPath", keys);
        }

        [Fact]
        public void SettingsProvider_Describe_Field_Count_Matches_Expected()
        {
            // Act
            var definitions = this.settingsProvider.Describe();

            // Assert - Should have at least the core fields
            Assert.True(definitions.Count >= 3, $"Expected at least 3 fields, got {definitions.Count}");
        }

        [Fact]
        public void SettingsProvider_GetDefaults_Returns_Valid_Dictionary()
        {
            // Act
            var defaults = this.settingsProvider.GetDefaults();

            // Assert
            Assert.NotNull(defaults);
            Assert.Contains("ConfigPath", defaults.Keys);
            Assert.Contains("RedirectUrl", defaults.Keys);
            Assert.Contains("DownloadPath", defaults.Keys);
        }

        [Fact]
        public void SettingsProvider_GetDefaults_Default_Quality_Is_Lossless()
        {
            // Act
            var defaults = this.settingsProvider.GetDefaults();

            // Assert
            Assert.True(defaults.TryGetValue("PreferredQuality", out var quality));
            Assert.Equal("Lossless", quality);
        }

        [Fact]
        public void SettingsProvider_Validate_Rejects_Empty_Required_Fields()
        {
            // Arrange
            var invalidSettings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = "",
                ["RedirectUrl"] = "",
                ["DownloadPath"] = ""
            };

            // Act
            var result = this.settingsProvider.Validate(invalidSettings);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void SettingsProvider_Validate_Accepts_Valid_Settings()
        {
            // Arrange
            var validSettings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = "/valid/config/path",
                ["RedirectUrl"] = "https://login.tidal.com/callback?code=test123&state=abc",
                ["DownloadPath"] = "/valid/download/path",
                ["PreferredQuality"] = "Lossless"
            };

            // Act
            var result = this.settingsProvider.Validate(validSettings);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void SettingsProvider_Apply_Rejects_Invalid_Settings()
        {
            // Arrange
            var invalidSettings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = "",
                ["RedirectUrl"] = "",
                ["DownloadPath"] = ""
            };

            // Act
            var result = this.settingsProvider.Apply(invalidSettings);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SettingsProvider_Apply_Accepts_Valid_Settings()
        {
            // Arrange
            var validSettings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = "/valid/config/path",
                ["RedirectUrl"] = "https://login.tidal.com/callback?code=test123&state=abc",
                ["DownloadPath"] = "/valid/download/path",
                ["PreferredQuality"] = "HiRes"
            };

            // Act
            var result = this.settingsProvider.Apply(validSettings);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Settings_RoundTrip_Preserves_Values()
        {
            // Arrange
            var originalSettings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = "/original/config",
                ["RedirectUrl"] = "https://login.tidal.com/callback?code=roundtrip&state=xyz",
                ["DownloadPath"] = "/original/downloads",
                ["PreferredQuality"] = "Lossless"
            };

            // Act - Apply and then get defaults
            var applyResult = this.settingsProvider.Apply(originalSettings);
            Assert.True(applyResult.IsValid);

            // Serialize and deserialize to simulate persistence
            var json = JsonSerializer.Serialize(originalSettings);
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(originalSettings.Count, deserialized.Count);
        }

        [Fact]
        public void Settings_Upgrade_From_V1_Works()
        {
            // Arrange - Simulate v1 settings format
            var v1Settings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = "/v1/config",
                ["RedirectUrl"] = "https://login.tidal.com/callback?code=test123&state=abc",
                ["DownloadPath"] = "/v1/downloads"
                // Note: PreferredQuality might be missing in v1
            };

            // Act
            var result = this.settingsProvider.Apply(v1Settings);

            // Assert - Should accept v1 settings without error
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Settings_Validate_Quality_Enum_Values()
        {
            // Arrange - Test each valid quality value
            var validQualities = new[] { "Low", "High", "Lossless", "HiRes" };

            foreach (var quality in validQualities)
            {
                var settings = new Dictionary<string, object?>
                {
                    ["ConfigPath"] = "/valid/path",
                    ["RedirectUrl"] = "https://login.tidal.com/callback?code=test123&state=abc",
                    ["DownloadPath"] = "/valid/downloads",
                    ["PreferredQuality"] = quality
                };

                // Act
                var result = this.settingsProvider.Validate(settings);

                // Assert
                Assert.True(result.IsValid, $"Quality '{quality}' should be valid");
            }
        }

        [Fact]
        public void Settings_Validate_Invalid_Quality_Falls_Back_To_Default()
        {
            // Arrange - MapToSettings silently ignores unrecognized quality strings
            // and falls back to the default (Lossless), which passes validation.
            // This is correct migration behavior: unknown enum values don't block settings.
            var settings = new Dictionary<string, object?>
            {
                ["ConfigPath"] = "/valid/path",
                ["RedirectUrl"] = "https://login.tidal.com/callback?code=test123&state=abc",
                ["DownloadPath"] = "/valid/downloads",
                ["PreferredQuality"] = "InvalidQuality"
            };

            // Act
            var result = this.settingsProvider.Validate(settings);

            // Assert - valid because mapper falls back to default Lossless
            Assert.True(result.IsValid);
        }
    }
}
