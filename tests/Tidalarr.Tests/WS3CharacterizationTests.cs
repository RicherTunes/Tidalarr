using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Integration;
using Lidarr.Plugin.Abstractions.Contracts;

namespace Tidalarr.Tests;

/// <summary>
/// Characterization tests for WS3.1 refactor - ensures key plugin invariants are preserved.
/// These tests document the expected behavior and prevent regressions during cleanup.
/// </summary>
public class WS3CharacterizationTests
{
    private sealed class TestPluginContext : IPluginContext
    {
        public Version HostVersion { get; } = new(2, 14, 2, 4786);
        public ILoggerFactory LoggerFactory { get; } = NullLoggerFactory.Instance;
        public IServiceProvider? Services { get; } = null;
    }

    #region TidalarrPlugin Invariants

    [Fact]
    public async Task Plugin_Initializes_WithoutThrow()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        Assert.NotNull(plugin.Manifest);
        Assert.Equal("tidalarr", plugin.Manifest.Id);
    }

    [Fact]
    public async Task Plugin_SettingsProvider_Describe_ReturnsRequiredFields()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        IReadOnlyCollection<SettingDefinition> definitions = plugin.SettingsProvider.Describe();

        Assert.Contains(definitions, d => d.Key == "ConfigPath");
        Assert.Contains(definitions, d => d.Key == "RedirectUrl");
        Assert.Contains(definitions, d => d.Key == "DownloadPath");
        Assert.Contains(definitions, d => d.Key == "PreferredQuality");
    }

    [Fact]
    public async Task Plugin_SettingsProvider_GetDefaults_ReturnsExpectedKeys()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        IReadOnlyDictionary<string, object?> defaults = plugin.SettingsProvider.GetDefaults();

        Assert.True(defaults.ContainsKey("ConfigPath"));
        Assert.True(defaults.ContainsKey("RedirectUrl"));
        Assert.True(defaults.ContainsKey("DownloadPath"));
        Assert.True(defaults.ContainsKey("PreferredQuality"));
    }

    [Fact]
    public async Task Plugin_SettingsProvider_Validate_ValidSettings_ReturnsSuccess()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        Dictionary<string, object?> settings = new()
        {
            ["ConfigPath"] = Path.GetTempPath(),
            ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=state",
            ["DownloadPath"] = Path.GetTempPath()
        };

        PluginValidationResult result = plugin.SettingsProvider.Validate(settings);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Plugin_SettingsProvider_Validate_InvalidSettings_ReturnsFailure()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        Dictionary<string, object?> settings = new()
        {
            ["ConfigPath"] = "",
            ["RedirectUrl"] = "not-a-valid-url",
            ["DownloadPath"] = ""
        };

        PluginValidationResult result = plugin.SettingsProvider.Validate(settings);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Plugin_SettingsProvider_Apply_ValidSettings_ReturnsSuccess()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        Dictionary<string, object?> settings = new()
        {
            ["ConfigPath"] = Path.GetTempPath(),
            ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=state",
            ["DownloadPath"] = Path.GetTempPath()
        };

        PluginValidationResult result = plugin.SettingsProvider.Apply(settings);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Plugin_CreateIndexerAsync_ReturnsNonNull()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        // Apply valid settings first
        Dictionary<string, object?> settings = new()
        {
            ["ConfigPath"] = Path.GetTempPath(),
            ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=state",
            ["DownloadPath"] = Path.GetTempPath()
        };
        plugin.SettingsProvider.Apply(settings);

        IIndexer? indexer = await plugin.CreateIndexerAsync();

        Assert.NotNull(indexer);
    }

    [Fact]
    public async Task Plugin_CreateDownloadClientAsync_ReturnsNonNull()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        // Apply valid settings first
        Dictionary<string, object?> settings = new()
        {
            ["ConfigPath"] = Path.GetTempPath(),
            ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=state",
            ["DownloadPath"] = Path.GetTempPath()
        };
        plugin.SettingsProvider.Apply(settings);

        IDownloadClient? downloadClient = await plugin.CreateDownloadClientAsync();

        Assert.NotNull(downloadClient);
    }

    [Fact]
    public async Task Plugin_DisposeAsync_DoesNotThrow()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        await plugin.DisposeAsync();
        // No assertion needed - test passes if no exception is thrown
    }

    #endregion

    #region TidalModule Invariants

    [Fact]
    public void TidalModule_RegisterServices_RegistersRequiredServices()
    {
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);

        ServiceProvider provider = services.BuildServiceProvider();

        // Verify key services are registered
        Assert.NotNull(provider.GetService<TidalIndexer>());
        Assert.NotNull(provider.GetService<TidalDownloadClient>());
    }

    [Fact]
    public void TidalModule_ValidateConfiguration_ValidSettings_ReturnsTrue()
    {
        TidalIndexerSettings settings = new()
        {
            ConfigPath = Path.GetTempPath(),
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=state"
        };

        bool isValid = TidalModule.ValidateConfiguration(settings);

        Assert.True(isValid);
    }

    [Fact]
    public void TidalModule_ValidateConfiguration_InvalidSettings_ReturnsFalse()
    {
        TidalIndexerSettings settings = new()
        {
            ConfigPath = "",
            RedirectUrl = ""
        };

        bool isValid = TidalModule.ValidateConfiguration(settings);

        Assert.False(isValid);
    }

    [Fact]
    public void TidalModule_ServiceName_ReturnsTidal()
    {
        TidalModule module = new();

        Assert.Equal("Tidal", module.ServiceName);
    }

    [Fact]
    public void TidalModule_HasIndexerAndDownloadClient_ReturnsTrue()
    {
        TidalModule module = new();
        Lidarr.Plugin.Common.Services.Registration.PluginMetadata metadata = module.GetMetadata();

        Assert.True(metadata.HasIndexer);
        Assert.True(metadata.HasDownloadClient);
    }

    #endregion
}
