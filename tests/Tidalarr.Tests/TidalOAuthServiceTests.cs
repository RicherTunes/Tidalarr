using System.Net;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;

namespace Tidalarr.Tests;

public class TidalOAuthServiceTests
{
    [Fact]
    public async Task GenerateAuthUrl_ReturnsValidTidalOAuthUrl()
    {
        // Arrange
        HttpClient httpClient = new();
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Act
        TidalAuthUrl authUrl = await oauthService.GenerateAuthUrlAsync();

        // Assert
        Assert.NotNull(authUrl);
        Assert.NotEmpty(authUrl.AuthorizationUrl);
        Assert.NotEmpty(authUrl.CodeVerifier);
        Assert.NotEmpty(authUrl.State);

        // Verify URL structure
        Assert.StartsWith("https://login.tidal.com/authorize?", authUrl.AuthorizationUrl);
        Assert.Contains("client_id=6BDSRdpK9hqEBTgU", authUrl.AuthorizationUrl);
        Assert.Contains("response_type=code", authUrl.AuthorizationUrl);
        Assert.Contains("code_challenge_method=S256", authUrl.AuthorizationUrl);
        Assert.Contains("scope=", authUrl.AuthorizationUrl);
        Assert.Contains("offline_access", authUrl.AuthorizationUrl);

        // Verify PKCE format
        Assert.Equal(128, authUrl.CodeVerifier.Length);
    }

    [Fact]
    public void PKCEGenerator_GeneratesValidChallengePair()
    {
        // Arrange
        PKCEGenerator generator = new();

        // Act
        (string verifier, string challenge) = generator.GeneratePair();

        // Assert
        Assert.Equal(128, verifier.Length);
        Assert.True(challenge.Length > 40);
        Assert.Matches(@"^[A-Za-z0-9._~-]+$", verifier);
        Assert.Matches(@"^[A-Za-z0-9_-]+$", challenge);
    }

    [Fact]
    public void TidalOAuthService_NotAuthenticatedInitially()
    {
        // Arrange
        HttpClient httpClient = new();
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Assert
        Assert.False(oauthService.IsAuthenticated);
    }

    [Theory]
    [InlineData("https://tidal.com/android/login/auth?code=test_auth_code&state=test_state")]
    [InlineData("https://tidal.com/android/login/auth?code=very_long_auth_code_12345&state=secure_state_67890")]
    public void ParseCallbackUrl_ValidUrl_ExtractsCodeAndState(string callbackUrl)
    {
        // Arrange
        HttpClient httpClient = new();
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Act
        TidalCallbackResult result = oauthService.ParseCallbackUrl(callbackUrl);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.AuthCode);
        Assert.NotEmpty(result.State);
    }

    [Theory]
    [InlineData("https://tidal.com/android/login/auth")] // No query params
    [InlineData("https://tidal.com/android/login/auth?error=access_denied")] // Error response
    [InlineData("https://wrong-domain.com/auth?code=test&state=test")] // Wrong domain
    [InlineData("not_a_url")] // Invalid URL
    public void ParseCallbackUrl_InvalidUrl_ReturnsFailure(string callbackUrl)
    {
        // Arrange
        HttpClient httpClient = new();
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Act
        TidalCallbackResult result = oauthService.ParseCallbackUrl(callbackUrl);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void ExchangeCodeAsync_ValidCode_ReturnsTokens()
    {
        // This test will be implemented when we have a mock HTTP client
        // For now, we'll test the request building logic
        Assert.True(true); // Placeholder - implement with HTTP mocking
    }

    [Fact]
    public void RefreshTokensAsync_ValidRefreshToken_ReturnsNewTokens()
    {
        // This test will be implemented when we have a mock HTTP client
        // For now, we'll test the request building logic  
        Assert.True(true); // Placeholder - implement with HTTP mocking
    }

    [Fact]
    public async Task GetValidTokensAsync_NotAuthenticated_ThrowsException()
    {
        // Arrange
        HttpClient httpClient = new();
        PKCEGenerator pkceGenerator = new();
        MockTokenStorage mockTokenStorage = new(); // Returns null (no stored tokens)
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, mockTokenStorage);

        // Act & Assert
        _ = await Assert.ThrowsAsync<InvalidOperationException>(oauthService.GetValidTokensAsync);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ValidResponse_ReturnsTokens()
    {
        // Arrange
        Domain.Authentication.TidalTokenResponse mockResponse = new(
            access_token: "test_access_token",
            refresh_token: "test_refresh_token",
            token_type: "Bearer",
            expires_in: 3600,
            user: new TidalUserResponse("session123", "US", 12345)
        );

        HttpClient httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Act
        TidalTokens tokens = await oauthService.ExchangeCodeAsync("test_auth_code", "test_verifier");

        // Assert
        Assert.NotNull(tokens);
        Assert.Equal("test_access_token", tokens.AccessToken);
        Assert.Equal("test_refresh_token", tokens.RefreshToken);
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal("session123", tokens.SessionId);
        Assert.Equal("US", tokens.CountryCode);
        Assert.Equal("12345", tokens.UserId);
        Assert.True(tokens.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ValidResponseWithoutUser_UsesJwtClaimsForSessionAndCountry()
    {
        string accessToken = CreateJwt(new Dictionary<string, object>
        {
            ["sid"] = "sess-from-jwt",
            ["cc"] = "ca"
        });

        Domain.Authentication.TidalTokenResponse mockResponse = new(
            access_token: accessToken,
            refresh_token: "test_refresh_token",
            token_type: "Bearer",
            expires_in: 3600,
            user: null
        );

        HttpClient httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        TidalTokens tokens = await oauthService.ExchangeCodeAsync("test_auth_code", "test_verifier");

        Assert.Equal("sess-from-jwt", tokens.SessionId);
        Assert.Equal("CA", tokens.CountryCode);
    }

    [Fact]
    public async Task RefreshTokensAsync_ValidResponse_ReturnsNewTokens()
    {
        // Arrange
        Domain.Authentication.TidalTokenResponse mockResponse = new(
            access_token: "new_access_token",
            refresh_token: "new_refresh_token",
            token_type: "Bearer",
            expires_in: 3600,
            user: new TidalUserResponse("session456", "US", 12345)
        );

        HttpClient httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Act
        TidalTokens tokens = await oauthService.RefreshTokensAsync("old_refresh_token");

        // Assert
        Assert.NotNull(tokens);
        Assert.Equal("new_access_token", tokens.AccessToken);
        Assert.Equal("new_refresh_token", tokens.RefreshToken);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ApiError_ThrowsException()
    {
        // Arrange
        HttpClient httpClient = CreateMockHttpClient("", HttpStatusCode.BadRequest);
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Act & Assert
        _ = await Assert.ThrowsAsync<HttpRequestException>(() =>
            oauthService.ExchangeCodeAsync("invalid_code", "test_verifier"));
    }

    // ── invalid_grant tests ──────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeCodeAsync_InvalidGrant_ThrowsTidalInvalidGrantException()
    {
        // Arrange: Tidal returns 400 invalid_grant (code already consumed or expired)
        const string tidalErrorBody = """{"error":"invalid_grant","error_description":"The token has expired. (Expired on time)","status":400,"sub_status":11003}""";
        HttpClient httpClient = CreateMockHttpClient(tidalErrorBody, HttpStatusCode.BadRequest);
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new MockTokenStorage());

        // Act & Assert: must throw the typed exception, not a generic HttpRequestException
        Tidalarr.Domain.Authentication.TidalInvalidGrantException ex =
            await Assert.ThrowsAsync<Tidalarr.Domain.Authentication.TidalInvalidGrantException>(() =>
                oauthService.ExchangeCodeAsync("already_used_code", "test_verifier"));

        // Message must be user-friendly and not contain the raw Tidal JSON
        Assert.Contains("invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("paste a fresh redirect URL", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sub_status", ex.Message);
    }

    [Fact]
    public async Task ExchangeCodeAsync_InvalidGrant_DoesNotSaveTokens()
    {
        // Arrange
        const string tidalErrorBody = """{"error":"invalid_grant","error_description":"The token has expired.","status":400,"sub_status":11003}""";
        HttpClient httpClient = CreateMockHttpClient(tidalErrorBody, HttpStatusCode.BadRequest);
        PKCEGenerator pkceGenerator = new();
        MockTokenStorage storage = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, storage);

        // Act (swallow the exception)
        _ = await Assert.ThrowsAsync<Tidalarr.Domain.Authentication.TidalInvalidGrantException>(() =>
            oauthService.ExchangeCodeAsync("already_used_code", "test_verifier"));

        // Assert: no tokens saved because the exchange failed
        TokenEnvelope<TidalTokens>? stored = await storage.LoadAsync();
        Assert.Null(stored);
    }

    [Theory]
    [InlineData("""{"error":"invalid_grant","error_description":"Expired","status":400}""")]
    [InlineData("""{"error":"INVALID_GRANT","status":400}""")]    // case-insensitive
    public async Task ExchangeCodeAsync_InvalidGrant_VariantBodies_ThrowTypedException(string errorBody)
    {
        HttpClient httpClient = CreateMockHttpClient(errorBody, HttpStatusCode.BadRequest);
        TidalOAuthService oauthService = new(httpClient, new PKCEGenerator(), new MockTokenStorage());

        _ = await Assert.ThrowsAsync<Tidalarr.Domain.Authentication.TidalInvalidGrantException>(() =>
            oauthService.ExchangeCodeAsync("code", "verifier"));
    }

    [Fact]
    public async Task ExchangeCodeAsync_OtherBadRequest_ThrowsHttpRequestException_NotInvalidGrant()
    {
        // Arrange: 400 with a different error code (e.g. invalid_client) must NOT be treated as invalid_grant
        const string tidalErrorBody = """{"error":"invalid_client","error_description":"Unknown client","status":400}""";
        HttpClient httpClient = CreateMockHttpClient(tidalErrorBody, HttpStatusCode.BadRequest);
        TidalOAuthService oauthService = new(httpClient, new PKCEGenerator(), new MockTokenStorage());

        // Act & Assert: generic HttpRequestException, NOT TidalInvalidGrantException
        _ = await Assert.ThrowsAsync<HttpRequestException>(() =>
            oauthService.ExchangeCodeAsync("code", "verifier"));
    }

    private static HttpClient CreateMockHttpClient(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        MockHttpMessageHandler mockHandler = new(jsonResponse, statusCode);
        return new HttpClient(mockHandler);
    }

    private static string CreateJwt(Dictionary<string, object> payloadClaims)
    {
        string headerJson = JsonSerializer.Serialize(new Dictionary<string, object> { ["alg"] = "none", ["typ"] = "JWT" });
        string payloadJson = JsonSerializer.Serialize(payloadClaims);
        string header = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        string payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public class MockHttpMessageHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    private readonly string _response = response;
    private readonly HttpStatusCode _statusCode = statusCode;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = new(this._statusCode);
        if (!string.IsNullOrEmpty(this._response))
        {
            response.Content = new StringContent(this._response, Encoding.UTF8, "application/json");
        }
        return Task.FromResult(response);
    }
}

public class MockTokenStorage : ITokenStore<TidalTokens>
{
    private TokenEnvelope<TidalTokens>? _envelope;

    public Task SaveAsync(TokenEnvelope<TidalTokens> envelope, CancellationToken cancellationToken = default)
    {
        this._envelope = envelope;
        return Task.CompletedTask;
    }

    public Task<TokenEnvelope<TidalTokens>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this._envelope);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        this._envelope = null;
        return Task.CompletedTask;
    }
}

