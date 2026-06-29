using System.Net;
using System.Text;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;

namespace Tidalarr.Tests;

/// <summary>
/// Regression guard for the "daily re-login" bug:
/// Tidal's grant_type=refresh_token endpoint does NOT return a refresh_token in its response
/// (standard OAuth — the client reuses the original). The old code wrote whatever the HTTP
/// response returned into the stored token, which destroyed the original refresh_token on
/// every renewal cycle.  Next time the access-token expired, GetValidTokensAsync saw
/// stored.RefreshToken == "" and fell through to throw "Not authenticated" → forced re-login.
///
/// The fix: in RefreshTokensCoreAsync, when the response omits refresh_token, carry forward
/// the refreshToken argument that was just successfully used.  Only warn when BOTH the response
/// AND the carried-forward token are empty (i.e. genuinely no refresh token anywhere).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Area", "E2E/Hermetic")]
public class TidalOAuthServiceRefreshTokenCarryForwardTests
{
    // Mimics Tidal's real refresh-token HTTP response: access_token renewed, no refresh_token field.
    private const string RefreshResponseNoRefreshToken =
        """{"access_token":"new_access_token","token_type":"Bearer","expires_in":3600}""";

    // ── Core carry-forward assertion ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokensAsync_WhenResponseOmitsRefreshToken_OriginalIsCarriedForward()
    {
        // Arrange: token store seeded with expired token that has a valid refresh_token
        TidalTokens expired = new(
            "old_access", "RT-ORIGINAL", "Bearer",
            DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        MemoryTokenStorage storage = new(expired);

        TidalOAuthService svc = new(
            new HttpClient(new CannedOkHandler(RefreshResponseNoRefreshToken)),
            storage);

        // Act: refresh using the stored refresh_token
        TidalTokens result = await svc.RefreshTokensAsync("RT-ORIGINAL");

        // Assert: access_token is updated, refresh_token is carried forward (not wiped)
        Assert.Equal("new_access_token", result.AccessToken);
        Assert.Equal("RT-ORIGINAL", result.RefreshToken);
        Assert.Equal("RT-ORIGINAL", storage.LastSavedTokens?.RefreshToken);
    }

    // ── Second-cycle regression guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokensAsync_SecondCycle_CarriedForwardTokenEnablesSecondRefresh()
    {
        // Arrange: two sequential HTTP calls; both omit refresh_token
        // Cycle 1 stores the carried-forward RT-ORIGINAL.
        // Cycle 2 reads that stored token and proves the bug is permanently gone.
        SequentialCannedHandler handler = new([
            """{"access_token":"new_access_1","token_type":"Bearer","expires_in":3600}""",
            """{"access_token":"new_access_2","token_type":"Bearer","expires_in":3600}""",
        ]);
        MemoryTokenStorage storage = new(null);
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        // Cycle 1: refresh using original refresh_token
        TidalTokens cycle1 = await svc.RefreshTokensAsync("RT-ORIGINAL");
        Assert.Equal("new_access_1", cycle1.AccessToken);
        Assert.Equal("RT-ORIGINAL", cycle1.RefreshToken);

        // Verify what was actually persisted — this is what GetValidTokensAsync will read next time
        string persistedRefreshToken = storage.LastSavedTokens?.RefreshToken ?? string.Empty;
        Assert.Equal("RT-ORIGINAL", persistedRefreshToken);

        // Cycle 2: simulate GetValidTokensAsync reading the persisted token on next expiry
        TidalTokens cycle2 = await svc.RefreshTokensAsync(persistedRefreshToken);
        Assert.Equal("new_access_2", cycle2.AccessToken);
        Assert.Equal("RT-ORIGINAL", cycle2.RefreshToken);
        Assert.Equal(2, handler.RequestCount); // two separate HTTP calls
    }

    // ── Rotation still honoured ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokensAsync_WhenResponseIncludesNewRefreshToken_RotationIsHonoured()
    {
        // When Tidal does rotate the refresh_token, the new one takes precedence over carry-forward
        const string rotatedResponse =
            """{"access_token":"new_access","refresh_token":"RT-ROTATED","token_type":"Bearer","expires_in":3600}""";
        MemoryTokenStorage storage = new(null);

        TidalOAuthService svc = new(
            new HttpClient(new CannedOkHandler(rotatedResponse)),
            storage);

        TidalTokens result = await svc.RefreshTokensAsync("RT-ORIGINAL");

        Assert.Equal("RT-ROTATED", result.RefreshToken);
        Assert.Equal("RT-ROTATED", storage.LastSavedTokens?.RefreshToken);
    }

    // ── Warn suppression on carry-forward ────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokensAsync_WhenResponseOmitsRefreshTokenAndOriginalIsPresent_DoesNotWarn()
    {
        // A missing refresh_token in a refresh RESPONSE is normal (OAuth spec); we carry forward.
        // No warning should be emitted when the carry-forward yields a valid token.
        List<string> warnings = new();
        TidalOAuthService svc = new(
            new HttpClient(new CannedOkHandler(RefreshResponseNoRefreshToken)),
            new MemoryTokenStorage(null),
            warnings.Add);

        _ = await svc.RefreshTokensAsync("RT-ORIGINAL");

        Assert.Empty(warnings);
    }

    // ── Warn still fires when genuinely no refresh token anywhere ────────────────────────────────

    [Fact]
    public async Task RefreshTokensAsync_WhenBothResponseAndOriginalHaveNoRefreshToken_Warns()
    {
        // Genuinely broken: offline_access scope missing, both response and original are empty.
        List<string> warnings = new();
        TidalOAuthService svc = new(
            new HttpClient(new CannedOkHandler(RefreshResponseNoRefreshToken)),
            new MemoryTokenStorage(null),
            warnings.Add);

        _ = await svc.RefreshTokensAsync(string.Empty);

        string warning = Assert.Single(warnings);
        Assert.Contains("offline_access", warning, StringComparison.OrdinalIgnoreCase);
    }

    // ── Exchange path unchanged ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeCodeAsync_WhenResponseOmitsRefreshToken_StillWarns()
    {
        // On the exchange path, a missing refresh_token is NOT normal — it means offline_access
        // was not granted.  The warning must still fire on the exchange path.
        const string exchangeResponseNoRefreshToken =
            """{"access_token":"acc","token_type":"Bearer","expires_in":3600}""";
        List<string> warnings = new();
        TidalOAuthService svc = new(
            new HttpClient(new CannedOkHandler(exchangeResponseNoRefreshToken)),
            new MemoryTokenStorage(null),
            warnings.Add);

        _ = await svc.ExchangeCodeAsync("auth_code", "verifier");

        string warning = Assert.Single(warnings);
        Assert.Contains("offline_access", warning, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Sequential handler: pops responses from a queue in order.</summary>
internal sealed class SequentialCannedHandler(IReadOnlyList<string> responses) : HttpMessageHandler
{
    private readonly IReadOnlyList<string> _responses = responses;
    private int _callIndex;

    public int RequestCount => _callIndex;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int idx = System.Threading.Interlocked.Increment(ref _callIndex) - 1;
        string body = idx < _responses.Count ? _responses[idx] : _responses[^1];
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
