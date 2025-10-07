using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Integration;
using Tidalarr.Integration.Diagnostics;
using Xunit;
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
        var plugin = new TidalarrPlugin();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "",
            ["RedirectUrl"] = "",
            ["DownloadPath"] = ""
        };

        var result = plugin.ApplySettingsWithDiagnostics(settings);
        Assert.False(result.Success);
        Assert.Equal("CFG100", result.Code);
        Assert.NotNull(result.Metadata["errors"]);
    }

    [Fact]
    public async Task ApplySettingsWithDiagnostics_Valid_ReturnsCFG000()
    {
        var plugin = new TidalarrPlugin();
        await plugin.InitializeAsync(new TestPluginContext(), CancellationToken.None);

        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = System.IO.Path.GetTempPath(),
            ["RedirectUrl"] = "https://tidal.com/android/login/auth?code=test&state=state",
            ["DownloadPath"] = System.IO.Path.GetTempPath()
        };

        var result = plugin.ApplySettingsWithDiagnostics(settings);
        Assert.True(result.Success);
        Assert.Equal("CFG000", result.Code);
    }
}

