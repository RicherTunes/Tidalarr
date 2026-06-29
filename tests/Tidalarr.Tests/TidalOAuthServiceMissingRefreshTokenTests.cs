using System.Net;
using System.Text;
using Tidalarr.Domain.Authentication;

namespace Tidalarr.Tests;

/// <summary>
/// Defensive guard: when Tidal returns a token WITHOUT a refresh_token (the classic symptom of an OAuth
/// scope missing <c>offline_access</c>), automatic session renewal is impossible and Tidalarr will force a
/// manual re-login when the access token expires — silently, weeks later. These pin that the OAuth service
/// emits a loud, actionable WARN at the moment such a token is obtained (exchange OR refresh), so a future
/// scope/Tidal regression is diagnosable at auth time instead of as a mysterious forced re-login.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Area", "E2E/Hermetic")]
public class TidalOAuthServiceMissingRefreshTokenTests
{
    private static string TokenJson(string refreshToken) =>
        $"{{\"access_token\":\"acc\",\"refresh_token\":\"{refreshToken}\",\"token_type\":\"Bearer\",\"expires_in\":3600}}";

    [Fact]
    public async Task ExchangeCode_WhenResponseHasNoRefreshToken_WarnsAutoRenewalDisabled()
    {
        List<string> warnings = new();
        TidalOAuthService svc = new(new HttpClient(new CannedOkHandler(TokenJson(string.Empty))), new MemoryTokenStorage(null), warnings.Add);

        _ = await svc.ExchangeCodeAsync("auth_code", "verifier");

        string warning = Assert.Single(warnings);
        Assert.Contains("offline_access", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("renew", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExchangeCode_WhenResponseHasRefreshToken_DoesNotWarn()
    {
        List<string> warnings = new();
        TidalOAuthService svc = new(new HttpClient(new CannedOkHandler(TokenJson("good_refresh"))), new MemoryTokenStorage(null), warnings.Add);

        _ = await svc.ExchangeCodeAsync("auth_code", "verifier");

        Assert.Empty(warnings);
    }

    [Fact]
    public async Task RefreshTokens_WhenResponseHasNoRefreshToken_AndOriginalNonEmpty_DoesNotWarn()
    {
        // Standard OAuth: grant_type=refresh_token responses routinely omit refresh_token.
        // When the original (carried-forward) token is non-empty the session is healthy —
        // no warning should be emitted. Only warn when BOTH are empty (offline_access missing).
        List<string> warnings = new();
        TidalOAuthService svc = new(new HttpClient(new CannedOkHandler(TokenJson(string.Empty))), new MemoryTokenStorage(null), warnings.Add);

        _ = await svc.RefreshTokensAsync("old_refresh");

        Assert.Empty(warnings);
    }

    [Fact]
    public async Task RefreshTokens_WhenResponseHasNoRefreshToken_AndOriginalAlsoEmpty_Warns()
    {
        // Genuinely broken: no refresh_token in either the response or the original token.
        // This is the symptom of offline_access scope not being granted.
        List<string> warnings = new();
        TidalOAuthService svc = new(new HttpClient(new CannedOkHandler(TokenJson(string.Empty))), new MemoryTokenStorage(null), warnings.Add);

        _ = await svc.RefreshTokensAsync(string.Empty);

        string warning = Assert.Single(warnings);
        Assert.Contains("offline_access", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshTokens_WhenResponseRotatesRefreshToken_DoesNotWarn()
    {
        List<string> warnings = new();
        TidalOAuthService svc = new(new HttpClient(new CannedOkHandler(TokenJson("rotated_refresh"))), new MemoryTokenStorage(null), warnings.Add);

        _ = await svc.RefreshTokensAsync("old_refresh");

        Assert.Empty(warnings);
    }
}

internal sealed class CannedOkHandler(string content) : HttpMessageHandler
{
    private readonly string content = content;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(this.content, Encoding.UTF8, "application/json"),
        });
}
