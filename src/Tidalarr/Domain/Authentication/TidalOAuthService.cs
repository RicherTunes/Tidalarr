using System.Net;
using System.Text;
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
using Lidarr.Plugin.Common.Interfaces;

namespace Tidalarr.Domain.Authentication;

public class TidalOAuthService : OAuthStreamingAuthenticationService<TidalTokens, TidalCredentials>, ITidalAuth, IStreamingTokenProvider
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

    // Backward-compatible overload used by existing tests/clients that passed a local PKCE generator
    public TidalOAuthService(HttpClient httpClient, PKCEGenerator _ /*unused*/, ITokenStorage? tokenStorage = null)
        : this(httpClient, tokenStorage) { }

    public bool IsAuthenticated => _currentTokens != null && !_currentTokens.IsExpired;

    public Task<TidalAuthUrl> GenerateAuthUrlAsync()
    {
        var (codeVerifier, codeChallenge) = _pkceGenerator.GeneratePair();
        var state = GenerateSecureState();
        var clientUniqueKey = GenerateClientUniqueKey(codeChallenge);
        var authUrl = BuildAuthorizationUrl(codeChallenge, state, clientUniqueKey);
        return Task.FromResult(new TidalAuthUrl(authUrl, codeVerifier, state, clientUniqueKey));
    }

    protected override async Task<TidalTokens> PerformAuthenticationAsync(TidalCredentials credentials)
    {
        var tokens = await GetValidTokensAsync();
        if (tokens == null)
            throw new InvalidOperationException("No valid tokens found. Complete OAuth flow first by calling GenerateAuthUrlAsync and ExchangeCodeAsync.");
        return tokens;
    }

    protected override Task<string> BuildAuthorizationUrlAsync(string codeChallenge, string state, string redirectUri, IEnumerable<string> scopes)
    {
        var clientUniqueKey = GenerateClientUniqueKey(codeChallenge);
        return Task.FromResult(BuildAuthorizationUrl(codeChallenge, state, clientUniqueKey));
    }

    protected override Task<TidalTokens> ExchangeCodeForTokensInternalAsync(string authorizationCode, string codeVerifier, string redirectUri)
        => ExchangeCodeAsync(authorizationCode, codeVerifier);

    protected override Task<TidalTokens> RefreshTokensInternalAsync(string refreshToken)
        => RefreshTokensAsync(refreshToken);

    protected override Task RevokeTokensInternalAsync(TidalTokens session)
        => LogoutAsync();

    protected override string ExtractRefreshToken(TidalTokens session)
        => session?.RefreshToken ?? string.Empty;

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
        Guard.NotNullOrWhiteSpace(authCode, nameof(authCode));
        Guard.NotNullOrWhiteSpace(codeVerifier, nameof(codeVerifier));

        var codeChallenge = _pkceGenerator.CreateS256Challenge(codeVerifier);
        var clientUniqueKey = GenerateClientUniqueKey(codeChallenge);

        var request = BuildTokenExchangeRequest(authCode, codeVerifier, clientUniqueKey);
        var (success, response) = await SafeOperationExecutor.TryExecuteAsync<HttpResponseMessage>(() => _httpClient.SendAsync(request));

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
            throw new InvalidOperationException("Failed to parse refresh token response");

        _currentTokens = MapToTidalTokens(tokenData);
        await _tokenStorage.SaveTokensAsync(_currentTokens);
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

    private string BuildAuthorizationUrl(string codeChallenge, string state, string clientUniqueKey)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["redirect_uri"] = TidalConstants.REDIRECT_URI,
            ["client_id"] = TidalConstants.CLIENT_ID_PKCE,
            ["lang"] = TidalConstants.LANGUAGE,
            ["appMode"] = TidalConstants.APP_MODE,
            ["client_unique_key"] = clientUniqueKey,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["restrict_signup"] = "true",
            ["state"] = state
        };

        var queryString = string.Join("&", parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{TidalConstants.LOGIN_BASE}?{queryString}";
    }

    private HttpRequestMessage BuildTokenExchangeRequest(string authCode, string codeVerifier, string clientUniqueKey)
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
            new KeyValuePair<string, string>("client_unique_key", clientUniqueKey),
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


    private static string GenerateClientUniqueKey(string codeChallenge)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeChallenge));
        var truncated = new byte[16];
        Array.Copy(hash, truncated, truncated.Length);
        return Convert.ToHexString(truncated).ToLowerInvariant();
    }

    private static TidalTokens MapToTidalTokens(TidalTokenResponse response)
        => new(
            AccessToken: response.access_token,
            RefreshToken: response.refresh_token,
            TokenType: response.token_type,
            ExpiresAt: DateTime.UtcNow.AddSeconds(response.expires_in),
            SessionId: response.user?.sessionId ?? string.Empty,
            CountryCode: response.user?.countryCode ?? "US",
            UserId: response.user?.userId.ToString() ?? string.Empty);

    public async Task<TidalTokens> GetValidTokensAsync()
    {
        if (_currentTokens != null && !_currentTokens.IsExpired)
            return _currentTokens;

        var stored = await _tokenStorage.LoadTokensAsync();
        if (stored != null && !stored.IsExpired)
        {
            _currentTokens = stored;
            return _currentTokens;
        }

        if (stored != null && stored.IsExpired && !string.IsNullOrEmpty(stored.RefreshToken))
        {
            var refreshed = await RefreshTokensAsync(stored.RefreshToken);
            _currentTokens = refreshed;
            return refreshed;
        }

        throw new InvalidOperationException("Not authenticated");
    }

    // IStreamingTokenProvider implementation for shared OAuth handler
    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            var tokens = await GetValidTokensAsync();
            return tokens.AccessToken;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> RefreshTokenAsync()
    {
        try
        {
            var stored = _currentTokens ?? await _tokenStorage.LoadTokensAsync();
            if (stored == null || string.IsNullOrEmpty(stored.RefreshToken)) return string.Empty;
            var refreshed = await RefreshTokensAsync(stored.RefreshToken);
            return refreshed.AccessToken;
        }
        catch
        {
            return string.Empty;
        }
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token)) return Task.FromResult(false);
        var valid = _currentTokens != null && !_currentTokens.IsExpired && _currentTokens.AccessToken == token;
        return Task.FromResult(valid);
    }

    public DateTime? GetTokenExpiration(string token)
    {
        if (_currentTokens != null && _currentTokens.AccessToken == token)
            return _currentTokens.ExpiresAt;
        return null;
    }

    public void ClearAuthenticationCache()
    {
        _currentTokens = null;
        try { _ = _tokenStorage.DeleteTokensAsync(); } catch { /* ignore */ }
    }

    public new bool SupportsRefresh => true;
    public string ServiceName => "Tidal";
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

