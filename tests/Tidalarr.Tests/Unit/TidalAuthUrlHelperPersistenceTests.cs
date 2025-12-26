using System.Text.Json;
using Tidalarr.Integration;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests.Unit;

public class TidalAuthUrlHelperPersistenceTests
{
    [Fact]
    public void GetAuthorizationUrl_WithConfigPath_PersistsPkceState()
    {
        string dir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            string url1 = TidalAuthUrlHelper.GetAuthorizationUrl(dir);
            string url2 = TidalAuthUrlHelper.GetAuthorizationUrl(dir);

            Assert.False(string.IsNullOrWhiteSpace(url1));
            Assert.Equal(url1, url2);
            Assert.StartsWith("https://login.tidal.com/authorize?", url1);
            Assert.Contains("code_challenge_method=S256", url1);

            string statePath = Path.Combine(dir, "tidal_pkce_state.json");
            Assert.True(File.Exists(statePath));

            PkceState? state = JsonSerializer.Deserialize<PkceState>(File.ReadAllText(statePath), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(state);
            Assert.False(string.IsNullOrWhiteSpace(state!.AuthorizationUrl));
            Assert.False(string.IsNullOrWhiteSpace(state.CodeVerifier));
            Assert.False(string.IsNullOrWhiteSpace(state.State));
            Assert.Equal(128, state.CodeVerifier.Length);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void GetAuthorizationUrl_WithEmptyConfigPath_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, TidalAuthUrlHelper.GetAuthorizationUrl(null));
        Assert.Equal(string.Empty, TidalAuthUrlHelper.GetAuthorizationUrl(""));
        Assert.Equal(string.Empty, TidalAuthUrlHelper.GetAuthorizationUrl("   "));
    }
}

