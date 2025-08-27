using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Tidalarr.Infrastructure.Storage;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Lidarr.Plugin.Common.Services;
using Lidarr.Plugin.Common.Services.Authentication;
using Lidarr.Plugin.Common.Utilities;

namespace Tidalarr.Domain.Authentication;

public class TidalOAuthService : OAuthStreamingAuthenticationService<TidalTokens, TidalCredentials>, ITidalAuth
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private TidalTokens? _currentTokens;
    
    public TidalOAuthService(HttpClient httpClient, ITokenStorage? tokenStorage = null)
        : base(new Lidarr.Plugin.Common.Services.Authentication.PKCEGenerator())
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage ?? new JsonTokenStorage();
    }
    
    public bool IsAuthenticated => _currentTokens != null && !_currentTokens.IsExpired;
    
    public Task<TidalAuthUrl> GenerateAuthUrlAsync()
    {
        var (codeVerifier, codeChallenge) = _pkceGenerator.GeneratePair();
        var state = GenerateSecureState();
        
        var authUrl = BuildAuthorizationUrl(codeChallenge, state);
        
        return Task.FromResult(new TidalAuthUrl(authUrl, codeVerifier, state));
    }

    // Implement base class abstract method
    protected override async Task<TidalTokens> PerformAuthenticationAsync(TidalCredentials credentials)
    {
        // For OAuth2 credentials, we can't authenticate directly without the authorization code
        // This method should be called after the OAuth flow is complete
        var tokens = await GetValidTokensAsync();
        if (tokens == null)
        {
            throw new InvalidOperationException("No valid tokens found. Complete OAuth flow first by calling GenerateAuthUrlAsync and ExchangeCodeAsync.");
        }
        return tokens;
    }

    // Override OAuth base class methods
    protected override async Task<string> BuildAuthorizationUrlAsync(string codeChallenge, string state, string redirectUri, IEnumerable<string> scopes)
    {
        return BuildAuthorizationUrl(codeChallenge, state);
    }

    protected override async Task<TidalTokens> ExchangeCodeForTokensInternalAsync(string authorizationCode, string codeVerifier, string redirectUri)
    {
        return await ExchangeCodeAsync(authorizationCode, codeVerifier);
    }

    protected override async Task<TidalTokens> RefreshTokensInternalAsync(string refreshToken)
    {
        return await RefreshTokensAsync(refreshToken);
    }

    protected override async Task RevokeTokensInternalAsync(TidalTokens session)
    {
        // Tidal doesn't have a specific revoke endpoint, just clear session
        await LogoutAsync();
    }

    protected override string ExtractRefreshToken(TidalTokens session)
    {
        return session?.RefreshToken ?? string.Empty;
    }

    protected override async Task CacheSessionAsync(TidalTokens session)
    {
        _currentTokens = session;
        await _tokenStorage.SaveTokensAsync(session);
    }

    protected override async Task ClearCachedSessionAsync()
    {
        _currentTokens = null;
        await _tokenStorage.DeleteTokensAsync();
    }
    
    public async Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
    {
        // Use shared library validation
        Guard.NotNullOrWhiteSpace(authCode, nameof(authCode));
        Guard.NotNullOrWhiteSpace(codeVerifier, nameof(codeVerifier));
        
        var request = BuildTokenExchangeRequest(authCode, codeVerifier);
        
        // Use shared library safe execution
        var (success, response) = await SafeOperationExecutor.TryExecuteAsync(() => 
            _httpClient.SendAsync(request));
            
        if (!success || response == null)
            throw new InvalidOperationException("Failed to exchange authorization code");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Token exchange failed: {response.StatusCode} - {errorContent}");
        }
        
        var content = await response.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<TidalTokenResponse>(content);
        
        if (tokenData == null)
            throw new InvalidOperationException("Failed to parse token response");
            
        _currentTokens = MapToTidalTokens(tokenData);
        
        // Save tokens for persistence
        await _tokenStorage.SaveTokensAsync(_currentTokens);
        
        return _currentTokens;
    }
    
    public async Task<TidalTokens> RefreshTokensAsync(string refreshToken)
    {
        var request = BuildTokenRefreshRequest(refreshToken);
        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Token refresh failed: {response.StatusCode} - {errorContent}");
        }
        
        var content = await response.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<TidalTokenResponse>(content);
        
        if (tokenData == null)
            throw new InvalidOperationException("Failed to parse refresh response");
            
        _currentTokens = MapToTidalTokens(tokenData);
        
        // Save refreshed tokens
        await _tokenStorage.SaveTokensAsync(_currentTokens);
        
        return _currentTokens;
    }
    
    public async Task<TidalTokens> GetValidTokensAsync()
    {
        // Try to load tokens from storage if not in memory
        if (_currentTokens == null)
        {
            _currentTokens = await _tokenStorage.LoadTokensAsync();
        }
        
        if (_currentTokens == null)
            throw new InvalidOperationException("Not authenticated");
            
        if (_currentTokens.IsExpired)
            _currentTokens = await RefreshTokensAsync(_currentTokens.RefreshToken);
            
        return _currentTokens;
    }
    
    public async Task LogoutAsync()
    {
        _currentTokens = null;
        await _tokenStorage.DeleteTokensAsync();
    }
    
    public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(callbackUrl))
                return TidalCallbackResult.Failure("Callback URL is empty");
                
            if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri))
                return TidalCallbackResult.Failure("Invalid URL format");
                
            if (!uri.Host.Equals("tidal.com", StringComparison.OrdinalIgnoreCase))
                return TidalCallbackResult.Failure("Invalid callback domain");
                
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            
            // Check for OAuth error response
            var error = queryParams.Get("error");
            if (!string.IsNullOrEmpty(error))
                return TidalCallbackResult.Failure($"OAuth error: {error}");
                
            var authCode = queryParams.Get("code");
            if (string.IsNullOrEmpty(authCode))
                return TidalCallbackResult.Failure("Authorization code not found in callback URL");
                
            var state = queryParams.Get("state");
            if (string.IsNullOrEmpty(state))
                return TidalCallbackResult.Failure("State parameter not found in callback URL");
                
            return TidalCallbackResult.Success(authCode, state);
        }
        catch (Exception ex)
        {
            return TidalCallbackResult.Failure($"Failed to parse callback URL: {ex.Message}");
        }
    }
    
    private string BuildAuthorizationUrl(string codeChallenge, string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["redirect_uri"] = TidalConstants.REDIRECT_URI,
            ["client_id"] = TidalConstants.CLIENT_ID_PKCE,
            ["lang"] = TidalConstants.LANGUAGE,
            ["appMode"] = TidalConstants.APP_MODE,
            ["client_unique_key"] = GenerateClientUniqueKey(),
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["restrict_signup"] = "true",
            ["state"] = state
        };
        
        var queryString = string.Join("&", parameters.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            
        return $"{TidalConstants.LOGIN_BASE}?{queryString}";
    }
    
    private HttpRequestMessage BuildTokenExchangeRequest(string authCode, string codeVerifier)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, TidalConstants.AUTH_BASE);
        
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", authCode),
            new KeyValuePair<string, string>("client_id", TidalConstants.CLIENT_ID_PKCE),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("redirect_uri", TidalConstants.REDIRECT_URI),
            new KeyValuePair<string, string>("scope", TidalConstants.OAUTH_SCOPE),
            new KeyValuePair<string, string>("code_verifier", codeVerifier),
            new KeyValuePair<string, string>("client_secret", TidalConstants.CLIENT_SECRET_PKCE)
        });
        
        request.Content = formData;
        return request;
    }
    
    private HttpRequestMessage BuildTokenRefreshRequest(string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, TidalConstants.AUTH_BASE);
        
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", TidalConstants.CLIENT_ID_PKCE),
            new KeyValuePair<string, string>("client_secret", TidalConstants.CLIENT_SECRET_PKCE)
        });
        
        request.Content = formData;
        return request;
    }
    
    protected override string GenerateSecureState()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("/", "_").Replace("+", "-").Replace("=", "");
    }
    
    private static string GenerateClientUniqueKey()
    {
        return Guid.NewGuid().ToString("N");
    }
    
    private static TidalTokens MapToTidalTokens(TidalTokenResponse response)
    {
        return new TidalTokens(
            AccessToken: response.access_token,
            RefreshToken: response.refresh_token,
            TokenType: response.token_type,
            ExpiresAt: DateTime.UtcNow.AddSeconds(response.expires_in),
            SessionId: response.user?.sessionId ?? string.Empty,
            CountryCode: response.user?.countryCode ?? "US",
            UserId: response.user?.userId.ToString() ?? string.Empty
        );
    }
}

public record TidalTokenResponse(
    string access_token,
    string refresh_token,
    string token_type,
    int expires_in,
    TidalUserResponse? user);
    
public record TidalUserResponse(
    string sessionId,
    string countryCode,
    long userId);
