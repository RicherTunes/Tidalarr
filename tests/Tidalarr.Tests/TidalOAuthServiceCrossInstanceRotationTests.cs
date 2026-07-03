using System.Net;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;

namespace Tidalarr.Tests;

/// <summary>
/// T-2 regression guard: Tidalarr runs two TidalOAuthService instances (indexer SP +
/// download-client SP) over the SAME token file, and Tidal rotates the refresh token on every use.
/// When both refresh concurrently, the loser gets invalid_grant for a token the winner already
/// rotated. The loser must NOT clear the store — that deletes the winner's fresh tokens and forces
/// a full re-login (the recurring "daily re-login" bug). It must instead adopt the rotated tokens.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Area", "E2E/Hermetic")]
public class TidalOAuthServiceCrossInstanceRotationTests
{
    private const string InvalidGrantBody = "{\"error\":\"invalid_grant\"}";

    [Fact]
    public async Task GetValidTokens_WhenAnotherInstanceAlreadyRotated_AdoptsWinnerTokens_DoesNotClear()
    {
        // The loser starts with the (now-dead) shared refresh token.
        TidalTokens dead = new("old_access", "dead_refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        RotatingTokenStorage storage = new(dead);

        // The winning instance's fresh, persisted, non-expired tokens.
        TidalTokens winner = new("winner_access", "winner_refresh", "Bearer", DateTime.UtcNow.AddHours(1), "sess-w", "US", "u1");

        // Handler simulates the loser's refresh: the winner has already rotated + persisted its
        // tokens by the time our (dead) refresh reaches Tidal, so Tidal returns invalid_grant.
        RotationRaceHandler handler = new(InvalidGrantBody, storage, winner);
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        TidalTokens result = await svc.GetValidTokensAsync();

        // The loser must adopt the winner's tokens, not throw or clear.
        Assert.Equal("winner_access", result.AccessToken);
        Assert.Equal("winner_refresh", result.RefreshToken);
        Assert.Equal(0, storage.ClearCount);

        // And the winner's tokens must survive on disk.
        TokenEnvelope<TidalTokens>? persisted = await storage.LoadAsync();
        Assert.NotNull(persisted);
        Assert.Equal("winner_refresh", persisted!.Session.RefreshToken);
        Assert.True(svc.IsAuthenticated);
    }

    [Fact]
    public async Task GetValidTokens_WhenGenuinelyRevoked_StillClears()
    {
        // No concurrent rotation: the on-disk token is still the dead one we tried. The revoked
        // token must still be cleared (preserving the fail-fast, no-retry-storm behavior).
        TidalTokens dead = new("old_access", "dead_refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        RotatingTokenStorage storage = new(dead); // no rotation configured
        RotationRaceHandler handler = new(InvalidGrantBody, storage, rotateTo: null);
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        _ = await Assert.ThrowsAsync<TidalInvalidGrantException>(svc.GetValidTokensAsync);

        Assert.True(storage.ClearCount >= 1);
        Assert.Null(await storage.LoadAsync());
        Assert.False(svc.IsAuthenticated);
    }
}

/// <summary>Token store double that supports observing clears and being rotated mid-flight.</summary>
internal sealed class RotatingTokenStorage : ITokenStore<TidalTokens>
{
    private TokenEnvelope<TidalTokens>? _envelope;

    public RotatingTokenStorage(TidalTokens? initial)
    {
        if (initial is not null)
        {
            this._envelope = new TokenEnvelope<TidalTokens>(initial, initial.ExpiresAt);
        }
    }

    public int ClearCount { get; private set; }

    public Task SaveAsync(TokenEnvelope<TidalTokens> envelope, CancellationToken cancellationToken = default)
    {
        this._envelope = envelope;
        return Task.CompletedTask;
    }

    public Task<TokenEnvelope<TidalTokens>?> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(this._envelope);

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCount++;
        this._envelope = null;
        return Task.CompletedTask;
    }

    // Simulates the winning instance persisting its freshly-rotated tokens.
    public void PersistWinner(TidalTokens winner)
        => this._envelope = new TokenEnvelope<TidalTokens>(winner, winner.ExpiresAt);
}

/// <summary>
/// Returns invalid_grant for the loser's refresh, first persisting the winner's rotated tokens into
/// the shared store (models "winner rotated + saved, then our stale refresh reaches Tidal").
/// </summary>
internal sealed class RotationRaceHandler(string content, RotatingTokenStorage storage, TidalTokens? rotateTo) : HttpMessageHandler
{
    private readonly string content = content;
    private readonly RotatingTokenStorage storage = storage;
    private readonly TidalTokens? rotateTo = rotateTo;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (this.rotateTo is not null)
        {
            this.storage.PersistWinner(this.rotateTo);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(this.content, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
