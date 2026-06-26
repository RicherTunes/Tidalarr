using System.Net;
using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;

namespace Tidalarr.Tests;

[Trait("Category", "Integration")]
[Trait("Area", "E2E/Hermetic")]
public class TidalOAuthServiceRevokedRefreshTokenTests
{
    private const string InvalidGrantBody = "{\"error\":\"invalid_grant\"}";

    [Fact]
    public async Task GetValidTokens_WhenRefreshReturnsInvalidGrant_ClearsPersistedTokensAndSurfacesReauthError()
    {
        TidalTokens expired = new("old_access", "dead_refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        MemoryTokenStorage storage = new(expired);
        InvalidGrantCountingHandler handler = new(InvalidGrantBody);
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        // A revoked/expired refresh token (HTTP 400 invalid_grant) must NOT throw a generic error.
        // It must clear the dead persisted token and surface an actionable re-authenticate message.
        TidalInvalidGrantException ex = await Assert.ThrowsAsync<TidalInvalidGrantException>(svc.GetValidTokensAsync);
        Assert.Contains("re-authenticate", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Persisted token store must be empty after a revoked-token rejection.
        TokenEnvelope<TidalTokens>? persisted = await storage.LoadAsync();
        Assert.Null(persisted);
        Assert.True(storage.ClearCount >= 1, "Revoked refresh token must clear the persisted token store.");
        Assert.False(svc.IsAuthenticated);
    }

    [Fact]
    public async Task GetValidTokens_AfterInvalidGrant_DoesNotRetryRefreshEndpoint()
    {
        TidalTokens expired = new("old_access", "dead_refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        MemoryTokenStorage storage = new(expired);
        InvalidGrantCountingHandler handler = new(InvalidGrantBody);
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        _ = await Assert.ThrowsAsync<TidalInvalidGrantException>(svc.GetValidTokensAsync);
        Assert.Equal(1, handler.RequestCount);

        // A second call must NOT hit the refresh endpoint again — the dead token was cleared,
        // so there is nothing to retry (no unbounded retry storm).
        _ = await Assert.ThrowsAsync<InvalidOperationException>(svc.GetValidTokensAsync);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetValidTokens_WhenRefreshReturnsTransientServerError_DoesNotClearTokens()
    {
        TidalTokens expired = new("old_access", "live_refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        MemoryTokenStorage storage = new(expired);
        InvalidGrantCountingHandler handler = new("{\"error\":\"server_error\"}", HttpStatusCode.ServiceUnavailable);
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        // A transient 503 is recoverable — the refresh token must SURVIVE for a later retry.
        _ = await Assert.ThrowsAsync<HttpRequestException>(svc.GetValidTokensAsync);

        TokenEnvelope<TidalTokens>? persisted = await storage.LoadAsync();
        Assert.NotNull(persisted);
        Assert.Equal("live_refresh", persisted!.Session.RefreshToken);
        Assert.Equal(0, storage.ClearCount);
    }

    [Fact]
    public async Task RefreshTokensAsync_WhenInvalidGrant_ThrowsTidalInvalidGrantException()
    {
        InvalidGrantCountingHandler handler = new(InvalidGrantBody);
        TidalOAuthService svc = new(new HttpClient(handler), new MemoryTokenStorage(null));

        // The direct-refresh path must mirror ExchangeCodeAsync: a 400 invalid_grant is a typed
        // exception, not a generic HttpRequestException.
        _ = await Assert.ThrowsAsync<TidalInvalidGrantException>(() => svc.RefreshTokensAsync("dead_refresh"));
    }

    [Fact]
    public async Task RefreshTokensAsync_WhenTransientServerError_ThrowsGenericHttpRequestException()
    {
        InvalidGrantCountingHandler handler = new("upstream down", HttpStatusCode.InternalServerError);
        TidalOAuthService svc = new(new HttpClient(handler), new MemoryTokenStorage(null));

        // Non-invalid_grant failures stay generic (transient) so callers do NOT clear tokens.
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => svc.RefreshTokensAsync("live_refresh"));
    }

    [Fact]
    [Trait("Area", "E2E/Hermetic")]
    public async Task StreamingTokenProviderRefresh_WhenInvalidGrant_ClearsPersistedTokenAndStopsRetrying()
    {
        TidalTokens expired = new("old_access", "dead_refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        MemoryTokenStorage storage = new(expired);
        InvalidGrantCountingHandler handler = new(InvalidGrantBody);
        TidalOAuthService svc = new(new HttpClient(handler), storage);
        IStreamingTokenProvider tokenProvider = svc;

        string accessToken = await tokenProvider.RefreshTokenAsync();

        Assert.Equal(string.Empty, accessToken);
        Assert.Null(await storage.LoadAsync());
        Assert.Equal(1, storage.ClearCount);
        Assert.Equal(1, handler.RequestCount);

        string secondAttempt = await tokenProvider.RefreshTokenAsync();

        Assert.Equal(string.Empty, secondAttempt);
        Assert.Equal(1, handler.RequestCount);
    }
}

internal sealed class InvalidGrantCountingHandler(string content, HttpStatusCode code = HttpStatusCode.BadRequest) : HttpMessageHandler
{
    private readonly string content = content;
    private readonly HttpStatusCode code = code;
    private int requestCount;

    public int RequestCount => Volatile.Read(ref this.requestCount);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref this.requestCount);
        return Task.FromResult(new HttpResponseMessage(this.code)
        {
            Content = new StringContent(this.content, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
