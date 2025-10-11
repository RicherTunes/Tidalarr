using System.Collections.Generic;
using Lidarr.Plugin.Abstractions.Results;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class DiagnosticsJsonTests
{
    [Fact]
    public void Settings_Success_Serializes_CorrectShape()
    {
        var payload = new Dictionary<string, string> { ["id"] = "CFG000", ["service"] = "Tidal" };
        var result = PluginOperationResult<Dictionary<string, string>>.Success(payload);
        var json = PluginOperationResultJson.ToJson(result);
        Assert.Contains("\"success\": true", json);
        Assert.Contains("\"id\": \"CFG000\"", json);
        Assert.Contains("\"service\": \"Tidal\"", json);
    }

    [Fact]
    public void Indexer_Unauthorized_Serializes_ErrorShape()
    {
        var error = new PluginError(PluginErrorCode.Unauthorized, "Authentication failed", null, new Dictionary<string, string> { ["id"] = "IX200", ["service"] = "Tidal" });
        var result = PluginOperationResult.Failure(error);
        var json = PluginOperationResultJson.ToJson(result);
        Assert.Contains("\"success\": false", json);
        Assert.Contains("\"code\": \"Unauthorized\"", json);
        Assert.Contains("\"id\": \"IX200\"", json);
    }

    [Fact]
    public void Download_ProviderUnavailable_Serializes_ErrorMetadata()
    {
        var error = new PluginError(PluginErrorCode.ProviderUnavailable, "Not authenticated", null, new Dictionary<string, string> { ["id"] = "DL100", ["trackId"] = "t1", ["quality"] = "Lossless" });
        var result = PluginOperationResult.Failure(error);
        var json = PluginOperationResultJson.ToJson(result);
        Assert.Contains("\"id\": \"DL100\"", json);
        Assert.Contains("\"trackId\": \"t1\"", json);
        Assert.Contains("\"quality\": \"Lossless\"", json);
    }
}

