using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests.Unit;

public class PKCEStateStoreTests
{
    [Fact]
    public void TryReadAuthorizationUrl_WithEmptyConfigPath_ReturnsNull()
    {
        Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(null));
        Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(string.Empty));
        Assert.Null(PKCEStateStore.TryReadAuthorizationUrl("   "));
    }

    [Fact]
    public void TryReadAuthorizationUrl_WithMissingStateFile_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryReadAuthorizationUrl_WithValidStateFile_ReturnsUrl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var path = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(path, """
                {
                  "authorizationUrl": "https://login.tidal.com/authorize?response_type=code",
                  "codeVerifier": "abc",
                  "state": "def",
                  "clientUniqueKey": "ghi",
                  "createdAt": "2025-01-01T00:00:00Z"
                }
                """);

            Assert.Equal("https://login.tidal.com/authorize?response_type=code", PKCEStateStore.TryReadAuthorizationUrl(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryReadAuthorizationUrl_WithInvalidJson_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var path = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(path, "{not json");

            Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

