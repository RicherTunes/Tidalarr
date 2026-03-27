using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Integration;

[Trait("Category", "Wave5")]
public class OAuthTokenProviderAdapterTests
{
    private static TidalTokens MakeTokens(
        string accessToken = "test-access-token",
        string refreshToken = "test-refresh-token",
        DateTime? expiresAt = null)
    {
        return new TidalTokens(
            accessToken,
            refreshToken,
            "Bearer",
            expiresAt ?? DateTime.UtcNow.AddHours(1),
            "sess-1",
            "US",
            "user-1");
    }

    // ---------------------------------------------------------------
    // Constructor null guard
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_NullAuth_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OAuthTokenProviderAdapter(null!));
    }

    // ---------------------------------------------------------------
    // Static properties
    // ---------------------------------------------------------------

    [Fact]
    public void SupportsRefresh_ReturnsTrue()
    {
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth());
        Assert.True(sut.SupportsRefresh);
    }

    [Fact]
    public void ServiceName_ReturnsTidal()
    {
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth());
        Assert.Equal("Tidal", sut.ServiceName);
    }

    // ---------------------------------------------------------------
    // GetAccessTokenAsync — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAccessTokenAsync_DelegatesToAuth_ReturnsAccessToken()
    {
        TidalTokens tokens = MakeTokens(accessToken: "happy-token");
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(tokens));

        string result = await sut.GetAccessTokenAsync();

        Assert.Equal("happy-token", result);
    }

    // ---------------------------------------------------------------
    // GetAccessTokenAsync — error path (logged, not thrown)
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAccessTokenAsync_WhenAuthThrows_ReturnsEmptyString()
    {
        OAuthTokenProviderAdapter sut = new(new ThrowingAuth());

        string result = await sut.GetAccessTokenAsync();

        Assert.Equal(string.Empty, result);
    }

    // ---------------------------------------------------------------
    // RefreshTokenAsync — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_DelegatesToAuth_ReturnsRefreshedAccessToken()
    {
        TidalTokens original = MakeTokens(refreshToken: "rt-1");
        TidalTokens refreshed = MakeTokens(accessToken: "refreshed-token");
        OAuthTokenProviderAdapter sut = new(new RefreshableAuth(original, refreshed));

        string result = await sut.RefreshTokenAsync();

        Assert.Equal("refreshed-token", result);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenIsEmpty_ReturnsEmptyString()
    {
        TidalTokens original = MakeTokens(refreshToken: "");
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(original));

        string result = await sut.RefreshTokenAsync();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenAuthThrows_ReturnsEmptyString()
    {
        OAuthTokenProviderAdapter sut = new(new ThrowingAuth());

        string result = await sut.RefreshTokenAsync();

        Assert.Equal(string.Empty, result);
    }

    // ---------------------------------------------------------------
    // ValidateTokenAsync — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task ValidateTokenAsync_WhenTokenMatchesCurrent_ReturnsTrue()
    {
        TidalTokens tokens = MakeTokens(accessToken: "valid-token");
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(tokens));

        bool result = await sut.ValidateTokenAsync("valid-token");

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenTokenDoesNotMatch_ReturnsFalse()
    {
        TidalTokens tokens = MakeTokens(accessToken: "current-token");
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(tokens));

        bool result = await sut.ValidateTokenAsync("stale-token");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenTokenIsNull_ReturnsFalse()
    {
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth());

        bool result = await sut.ValidateTokenAsync(null!);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenTokenIsEmpty_ReturnsFalse()
    {
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth());

        bool result = await sut.ValidateTokenAsync(string.Empty);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenAuthThrows_ReturnsFalse()
    {
        OAuthTokenProviderAdapter sut = new(new ThrowingAuth());

        bool result = await sut.ValidateTokenAsync("any-token");

        Assert.False(result);
    }

    // ---------------------------------------------------------------
    // GetTokenExpiration — cache behavior
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetTokenExpiration_AfterGetAccessToken_ReturnsCachedExpiry()
    {
        DateTime expiry = DateTime.UtcNow.AddHours(2);
        TidalTokens tokens = MakeTokens(accessToken: "tok", expiresAt: expiry);
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(tokens));

        // Prime the cache via GetAccessTokenAsync
        await sut.GetAccessTokenAsync();

        DateTime? result = sut.GetTokenExpiration("tok");
        Assert.Equal(expiry, result);
    }

    [Fact]
    public void GetTokenExpiration_WithoutPrior_ReturnsNull()
    {
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth());

        DateTime? result = sut.GetTokenExpiration("unknown");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTokenExpiration_WithWrongToken_ReturnsNull()
    {
        TidalTokens tokens = MakeTokens(accessToken: "real-token");
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(tokens));
        await sut.GetAccessTokenAsync();

        DateTime? result = sut.GetTokenExpiration("wrong-token");
        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // ClearAuthenticationCache
    // ---------------------------------------------------------------

    [Fact]
    public async Task ClearAuthenticationCache_ClearsCachedExpiry()
    {
        DateTime expiry = DateTime.UtcNow.AddHours(2);
        TidalTokens tokens = MakeTokens(accessToken: "tok", expiresAt: expiry);
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(tokens));

        await sut.GetAccessTokenAsync();
        Assert.NotNull(sut.GetTokenExpiration("tok"));

        sut.ClearAuthenticationCache();

        Assert.Null(sut.GetTokenExpiration("tok"));
    }

    // ---------------------------------------------------------------
    // Cache is updated by RefreshTokenAsync and ValidateTokenAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_UpdatesCacheWithRefreshedToken()
    {
        DateTime refreshedExpiry = DateTime.UtcNow.AddHours(3);
        TidalTokens original = MakeTokens(refreshToken: "rt-1");
        TidalTokens refreshed = MakeTokens(accessToken: "new-tok", expiresAt: refreshedExpiry);
        OAuthTokenProviderAdapter sut = new(new RefreshableAuth(original, refreshed));

        await sut.RefreshTokenAsync();

        Assert.Equal(refreshedExpiry, sut.GetTokenExpiration("new-tok"));
    }

    [Fact]
    public async Task ValidateTokenAsync_UpdatesCache()
    {
        DateTime expiry = DateTime.UtcNow.AddHours(2);
        TidalTokens tokens = MakeTokens(accessToken: "tok", expiresAt: expiry);
        OAuthTokenProviderAdapter sut = new(new HappyPathAuth(tokens));

        await sut.ValidateTokenAsync("tok");

        Assert.Equal(expiry, sut.GetTokenExpiration("tok"));
    }

    // ===============================================================
    // Test stubs
    // ===============================================================

    private sealed class HappyPathAuth : ITidalAuth
    {
        private readonly TidalTokens _tokens;

        public HappyPathAuth() : this(MakeTokens()) { }
        public HappyPathAuth(TidalTokens tokens) => _tokens = tokens;

        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() =>
            Task.FromResult(new TidalAuthUrl("", "", "", ""));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) =>
            Task.FromResult(_tokens);
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) =>
            Task.FromResult(_tokens);
        public Task<TidalTokens> GetValidTokensAsync() =>
            Task.FromResult(_tokens);
        public TidalCallbackResult ParseCallbackUrl(string callbackUrl) =>
            TidalCallbackResult.Failure("stub");
    }

    private sealed class ThrowingAuth : ITidalAuth
    {
        public bool IsAuthenticated => false;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() =>
            throw new InvalidOperationException("auth failure");
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) =>
            throw new InvalidOperationException("auth failure");
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) =>
            throw new InvalidOperationException("auth failure");
        public Task<TidalTokens> GetValidTokensAsync() =>
            throw new InvalidOperationException("auth failure");
        public TidalCallbackResult ParseCallbackUrl(string callbackUrl) =>
            TidalCallbackResult.Failure("auth failure");
    }

    /// <summary>
    /// Auth stub that returns different tokens for GetValidTokensAsync vs RefreshTokensAsync.
    /// Simulates the two-step refresh flow: first GetValidTokens gets the original (with refresh token),
    /// then RefreshTokensAsync returns the refreshed tokens.
    /// </summary>
    private sealed class RefreshableAuth : ITidalAuth
    {
        private readonly TidalTokens _original;
        private readonly TidalTokens _refreshed;

        public RefreshableAuth(TidalTokens original, TidalTokens refreshed)
        {
            _original = original;
            _refreshed = refreshed;
        }

        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() =>
            Task.FromResult(new TidalAuthUrl("", "", "", ""));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) =>
            Task.FromResult(_original);
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) =>
            Task.FromResult(_refreshed);
        public Task<TidalTokens> GetValidTokensAsync() =>
            Task.FromResult(_original);
        public TidalCallbackResult ParseCallbackUrl(string callbackUrl) =>
            TidalCallbackResult.Failure("stub");
    }
}
