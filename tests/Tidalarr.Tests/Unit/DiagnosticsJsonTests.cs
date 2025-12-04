using Lidarr.Plugin.Abstractions.Results;

namespace Tidalarr.Tests.Unit;

public class DiagnosticsJsonTests
{
    [Fact]
    public void Settings_Success_Serializes_CorrectShape()
    {
        Dictionary<string, string> payload = new() { ["id"] = "CFG000", ["service"] = "Tidal" };
        PluginOperationResult<Dictionary<string, string>> result = PluginOperationResult<Dictionary<string, string>>.Success(payload);
        string json = PluginOperationResultJson.ToJson(result);
        Assert.Contains("\"success\": true", json);
        Assert.Contains("\"id\": \"CFG000\"", json);
        Assert.Contains("\"service\": \"Tidal\"", json);
    }

    [Fact]
    public void Indexer_Unauthorized_Serializes_ErrorShape()
    {
        PluginError error = new(PluginErrorCode.Unauthorized, "Authentication failed", null, new Dictionary<string, string> { ["id"] = "IX200", ["service"] = "Tidal" });
        PluginOperationResult result = PluginOperationResult.Failure(error);
        string json = PluginOperationResultJson.ToJson(result);
        Assert.Contains("\"success\": false", json);
        Assert.Contains("\"code\": \"Unauthorized\"", json);
        Assert.Contains("\"id\": \"IX200\"", json);
    }

    [Fact]
    public void Download_ProviderUnavailable_Serializes_ErrorMetadata()
    {
        PluginError error = new(PluginErrorCode.ProviderUnavailable, "Not authenticated", null, new Dictionary<string, string> { ["id"] = "DL100", ["trackId"] = "t1", ["quality"] = "Lossless" });
        PluginOperationResult result = PluginOperationResult.Failure(error);
        string json = PluginOperationResultJson.ToJson(result);
        Assert.Contains("\"id\": \"DL100\"", json);
        Assert.Contains("\"trackId\": \"t1\"", json);
        Assert.Contains("\"quality\": \"Lossless\"", json);
    }
}

