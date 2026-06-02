using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;
using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Regression guard for the Tidal OAuth re-auth loop (fix/tidal-oauth-state-loop).
///
/// Bug: in a manual copy/paste OAuth flow the redirect URL the user pastes carries the
/// `state` from whatever auth URL they opened. Every indexer Test (Lidarr's periodic
/// auto-tests + manual clicks) used to compare that `state` against the stored PKCE state
/// and, on any mismatch, call <see cref="PKCEStateStore.RegenerateCodes(string)"/> — minting
/// a brand-new code_verifier / code_challenge / state. That invalidated whatever auth URL the
/// user had open before they could finish the exchange, so re-auth could never converge
/// ("OAuth state mismatch" forever).
///
/// The CSRF `state` check is meaningless in a manual paste flow (the user IS the redirect
/// channel); PKCE's code_verifier↔code_challenge binding is the real security control and is
/// fully preserved. The fix removes the mid-flow state-mismatch validate+regenerate block so a
/// mismatched-state callback proceeds straight to the token exchange using the STORED verifier.
///
/// These tests construct the real <see cref="TidalLidarrIndexer"/> (mocking the four fixed
/// HttpIndexerBase host services) and drive the internal <c>TryExchangeAuthorizationCode</c>
/// seam against a real temp-dir-backed <see cref="PKCEStateStore"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Auth")]
public sealed class TidalLidarrIndexerOAuthStateExchangeTests : IDisposable
{
    private readonly string _configPath;

    public TidalLidarrIndexerOAuthStateExchangeTests()
    {
        _configPath = Path.Combine(Path.GetTempPath(), "tidalarr-oauth-state-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_configPath))
            {
                Directory.Delete(_configPath, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static TidalLidarrIndexer BuildIndexer(TidalLidarrIndexerSettings settings)
    {
        var httpClient = new Mock<IHttpClient>(MockBehavior.Loose);
        var statusService = new Mock<IIndexerStatusService>(MockBehavior.Loose);
        var configService = new Mock<IConfigService>(MockBehavior.Loose);
        var parsingService = new Mock<IParsingService>(MockBehavior.Loose);
        Logger logger = LogManager.GetCurrentClassLogger();

        return new TidalLidarrIndexer(
            httpClient.Object,
            statusService.Object,
            configService.Object,
            parsingService.Object,
            logger)
        {
            Definition = new IndexerDefinition { Settings = settings },
        };
    }

    private async Task<PKCEState> SeedStoredStateAsync(string state, string verifier)
    {
        // Persist a stored PKCE state with a known state + code_verifier through the real store.
        var store = new PKCEStateStore(_configPath);
        var stored = new PKCEState(
            AuthorizationUrl: "https://login.tidal.com/authorize?state=" + state,
            CodeVerifier: verifier,
            State: state,
            ClientUniqueKey: "client-unique-key",
            CreatedAt: DateTime.UtcNow);
        await store.SaveStateAsync(stored);
        return stored;
    }

    /// <summary>
    /// THE regression: a callback whose `state` does NOT match the stored PKCE state must NOT
    /// abort/regenerate — it must proceed to attempt the token exchange with the STORED verifier.
    /// Before the fix this returned false and never called ExchangeCodeAsync.
    /// </summary>
    [Fact]
    public async Task TryExchangeAuthorizationCode_WhenStateMismatch_ProceedsToExchangeWithStoredVerifier()
    {
        const string storedState = "STORED_STATE_ABC";
        const string storedVerifier = "STORED_VERIFIER_XYZ";
        const string callbackState = "DIFFERENT_STATE_FROM_A_STALE_OR_OTHER_TAB";
        const string authCode = "AUTH_CODE_123";

        _ = await SeedStoredStateAsync(storedState, storedVerifier);

        var settings = new TidalLidarrIndexerSettings
        {
            ConfigPath = _configPath,
            RedirectUrl = "https://tidal.com/android/login/auth?code=" + authCode + "&state=" + callbackState,
            TidalMarket = "US",
        };

        var auth = new Mock<ITidalAuth>(MockBehavior.Strict);
        auth.Setup(a => a.ParseCallbackUrl(settings.RedirectUrl))
            .Returns(TidalCallbackResult.Success(authCode, callbackState));

        string? capturedVerifier = null;
        auth.Setup(a => a.ExchangeCodeAsync(authCode, It.IsAny<string>()))
            .Callback<string, string>((_, verifier) => capturedVerifier = verifier)
            .ReturnsAsync(new TidalTokens(
                AccessToken: "access-token",
                RefreshToken: "refresh-token",
                TokenType: "Bearer",
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                SessionId: "sid",
                CountryCode: "US",
                UserId: "uid"));

        TidalLidarrIndexer indexer = BuildIndexer(settings);
        var failures = new List<ValidationFailure>();

        bool result = await indexer.TryExchangeAuthorizationCode(auth.Object, failures);

        Assert.True(result, "State mismatch must NOT short-circuit; the exchange should proceed and succeed.");
        auth.Verify(a => a.ExchangeCodeAsync(authCode, It.IsAny<string>()), Times.Once);
        Assert.Equal(storedVerifier, capturedVerifier);
        Assert.Empty(failures);
    }

    /// <summary>
    /// The stored PKCE state must remain STABLE while the user is mid-flow on a mismatched-state
    /// Test (no destructive regenerate before the exchange). Stability across Tests is what lets the
    /// real flow converge: the auth URL the user opened stays valid until they paste the redirect.
    /// </summary>
    [Fact]
    public async Task TryExchangeAuthorizationCode_WhenStateMismatch_DoesNotRegenerateStoredVerifierBeforeExchange()
    {
        const string storedState = "STABLE_STATE";
        const string storedVerifier = "STABLE_VERIFIER";
        const string callbackState = "MISMATCHED_STATE";
        const string authCode = "CODE_999";

        _ = await SeedStoredStateAsync(storedState, storedVerifier);

        var settings = new TidalLidarrIndexerSettings
        {
            ConfigPath = _configPath,
            RedirectUrl = "https://tidal.com/android/login/auth?code=" + authCode + "&state=" + callbackState,
            TidalMarket = "US",
        };

        var auth = new Mock<ITidalAuth>(MockBehavior.Strict);
        auth.Setup(a => a.ParseCallbackUrl(settings.RedirectUrl))
            .Returns(TidalCallbackResult.Success(authCode, callbackState));

        // The verifier observed at exchange time proves the stored state was NOT regenerated
        // (regeneration would have replaced the verifier with a fresh random value).
        string? capturedVerifier = null;
        auth.Setup(a => a.ExchangeCodeAsync(authCode, It.IsAny<string>()))
            .Callback<string, string>((_, verifier) => capturedVerifier = verifier)
            .ReturnsAsync(new TidalTokens(
                AccessToken: "access-token",
                RefreshToken: "refresh-token",
                TokenType: "Bearer",
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                SessionId: "sid",
                CountryCode: "US",
                UserId: "uid"));

        TidalLidarrIndexer indexer = BuildIndexer(settings);

        _ = await indexer.TryExchangeAuthorizationCode(auth.Object, new List<ValidationFailure>());

        Assert.Equal(storedVerifier, capturedVerifier);
    }
}
