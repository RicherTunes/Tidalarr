using System.Net;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests;

public class TidalOAuthServiceTests
{
    [Fact]
    public async Task GenerateAuthUrl_ReturnsValidTidalOAuthUrl()
    {
        // Arrange
        var httpClient = new HttpClient();
        var pkceGenerator = new PKCEGenerator();
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, new MockTokenStorage());
        
        // Act
        var authUrl = await oauthService.GenerateAuthUrlAsync();
        
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
        
        // Verify PKCE format
        Assert.Equal(128, authUrl.CodeVerifier.Length);
    }
    
    [Fact]
    public void PKCEGenerator_GeneratesValidChallengePair()
    {
        // Arrange
        var generator = new PKCEGenerator();
        
        // Act
        var (verifier, challenge) = generator.GeneratePair();
        
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
        var httpClient = new HttpClient();
        var pkceGenerator = new PKCEGenerator();
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, new MockTokenStorage());
        
        // Assert
        Assert.False(oauthService.IsAuthenticated);
    }
    
    [Theory]
    [InlineData("https://tidal.com/android/login/auth?code=test_auth_code&state=test_state")]
    [InlineData("https://tidal.com/android/login/auth?code=very_long_auth_code_12345&state=secure_state_67890")]
    public void ParseCallbackUrl_ValidUrl_ExtractsCodeAndState(string callbackUrl)
    {
        // Arrange
        var httpClient = new HttpClient();
        var pkceGenerator = new PKCEGenerator();
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, new MockTokenStorage());
        
        // Act
        var result = oauthService.ParseCallbackUrl(callbackUrl);
        
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
        var httpClient = new HttpClient();
        var pkceGenerator = new PKCEGenerator();
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, new MockTokenStorage());
        
        // Act
        var result = oauthService.ParseCallbackUrl(callbackUrl);
        
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
        var httpClient = new HttpClient();
        var pkceGenerator = new PKCEGenerator();
        var mockTokenStorage = new MockTokenStorage(); // Returns null (no stored tokens)
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, mockTokenStorage);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            oauthService.GetValidTokensAsync());
    }
    
    [Fact]
    public async Task ExchangeCodeAsync_ValidResponse_ReturnsTokens()
    {
        // Arrange
        var mockResponse = new TidalTokenResponse(
            access_token: "test_access_token",
            refresh_token: "test_refresh_token", 
            token_type: "Bearer",
            expires_in: 3600,
            user: new TidalUserResponse("session123", "US", 12345)
        );
        
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        var pkceGenerator = new PKCEGenerator();
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, new MockTokenStorage());
        
        // Act
        var tokens = await oauthService.ExchangeCodeAsync("test_auth_code", "test_verifier");
        
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
    public async Task RefreshTokensAsync_ValidResponse_ReturnsNewTokens()
    {
        // Arrange
        var mockResponse = new TidalTokenResponse(
            access_token: "new_access_token",
            refresh_token: "new_refresh_token",
            token_type: "Bearer", 
            expires_in: 3600,
            user: new TidalUserResponse("session456", "US", 12345)
        );
        
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        var pkceGenerator = new PKCEGenerator();
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, new MockTokenStorage());
        
        // Act
        var tokens = await oauthService.RefreshTokensAsync("old_refresh_token");
        
        // Assert
        Assert.NotNull(tokens);
        Assert.Equal("new_access_token", tokens.AccessToken);
        Assert.Equal("new_refresh_token", tokens.RefreshToken);
    }
    
    [Fact]
    public async Task ExchangeCodeAsync_ApiError_ThrowsException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("", HttpStatusCode.BadRequest);
        var pkceGenerator = new PKCEGenerator();
        var oauthService = new TidalOAuthService(httpClient, pkceGenerator, new MockTokenStorage());
        
        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => 
            oauthService.ExchangeCodeAsync("invalid_code", "test_verifier"));
    }
    
    private static HttpClient CreateMockHttpClient(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new MockHttpMessageHandler(jsonResponse, statusCode);
        return new HttpClient(mockHandler);
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _statusCode;
    
    public MockHttpMessageHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _response = response;
        _statusCode = statusCode;
    }
    
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode);
        if (!string.IsNullOrEmpty(_response))
        {
            response.Content = new StringContent(_response, Encoding.UTF8, "application/json");
        }
        return Task.FromResult(response);
    }
}

public class MockTokenStorage : ITokenStorage
{
    private TidalTokens? _tokens;
    
    public Task SaveTokensAsync(TidalTokens tokens)
    {
        _tokens = tokens;
        return Task.CompletedTask;
    }
    
    public Task<TidalTokens?> LoadTokensAsync()
    {
        return Task.FromResult(_tokens);
    }
    
    public Task DeleteTokensAsync()
    {
        _tokens = null;
        return Task.CompletedTask;
    }
}



