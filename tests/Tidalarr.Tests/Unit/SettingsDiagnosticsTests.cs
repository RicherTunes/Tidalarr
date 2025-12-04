using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Integration;
using Lidarr.Plugin.Abstractions.Results;
using Lidarr.Plugin.Abstractions.Contracts;

namespace Tidalarr.Tests.Unit;

public class SettingsDiagnosticsTests
{
    private sealed class TestPluginContext : IPluginContext
    {
        public Version HostVersion { get; } = new(2, 14, 2, 4786);
        public ILoggerFactory LoggerFactory { get; } = NullLoggerFactory.Instance;
        public IServiceProvider? Services { get; } = null;
    }

    [Fact]
    public async Task ApplySettingsWithDiagnostics_Invalid_ReturnsCFG100_WithCodes()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        Dictionary<string, object?> settings = new()
        {
            ["ConfigPath"] = "",
            ["RedirectUrl"] = "",
            ["DownloadPath"] = ""
        };

        PluginOperationResult<Dictionary<string, string>> result = plugin.ApplySettingsWithDiagnostics(settings);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PluginErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Equal("CFG100", result.Error!.Metadata["id"]);
        Assert.True(result.Error!.Metadata.ContainsKey("errors"));
    }

    [Fact]
    public async Task ApplySettingsWithDiagnostics_Valid_ReturnsCFG000()
    {
        TidalarrPlugin plugin = new();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        Dictionary<string, object?> settings = new()
        {
            ["ConfigPath"] = Path.GetTempPath(),
            ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=state",
            ["DownloadPath"] = Path.GetTempPath()
        };

        PluginOperationResult<Dictionary<string, string>> result = plugin.ApplySettingsWithDiagnostics(settings);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("CFG000", result.Value!["id"]);
    }
}
