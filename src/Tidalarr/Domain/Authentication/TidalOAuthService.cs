using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Tidalarr.Infrastructure.Storage;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Lidarr.Plugin.Common.Observability;
using Lidarr.Plugin.Common.Services;
using Lidarr.Plugin.Common.Services.Authentication;
using Lidarr.Plugin.Common.Utilities;
using Lidarr.Plugin.Common.Interfaces;

namespace Tidalarr.Domain.Authentication;

public class TidalOAuthService(HttpClient httpClient, ITokenStore<TidalTokens>? tokenStorage = null) : OAuthStreamingAuthenticationService<TidalTokens, TidalCredentials>(new PKCEGenerator()), ITidalAuth, IStreamingTokenProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStore<TidalTokens> _tokenStorage = tokenStorage ?? new FailOnIOTokenStore<TidalTokens>();
    private TidalTokens? _currentTokens;

    // Backward-compatible overload used by existing tests/clients that passed a PKCE generator
    public TidalOAuthService(HttpClient httpClient, IPKCEGenerator _ /*unused*/, ITokenStore<TidalTokens>? tokenStorage = null)
        : this(httpClient, tokenStorage) { }

    public bool IsAuthenticated => this._currentTokens != null && !this._currentTokens.IsExpired;

    public Task<TidalAuthUrl> GenerateAuthUrlAsync()
    {
        (string codeVerifier, string codeChallenge) = this._pkceGenerator.GeneratePair();
        string state = GenerateSecureState();
        string clientUniqueKey = GenerateClientUniqueKey(codeChallenge);
        string authUrl = BuildAuthorizationUrl(codeChallenge, state, clientUniqueKey, TidalConstants.OAUTH_SCOPE);
        return Task.FromResult(new TidalAuthUrl(authUrl, codeVerifier, state, clientUniqueKey));
    }

    protected override async Task<TidalTokens> PerformAuthenticationAsync(TidalCredentials credentials)
    {
        TidalTokens tokens = await GetValidTokensAsync().ConfigureAwait(false);
        return tokens ?? throw new InvalidOperationException("No valid tokens found. Complete OAuth flow first by calling GenerateAuthUrlAsync and ExchangeCodeAsync.");
    }

    protected override Task<string> BuildAuthorizationUrlAsync(string codeChallenge, string state, string redirectUri, IEnumerable<string> scopes)
    {
        string clientUniqueKey = GenerateClientUniqueKey(codeChallenge);
        string scopeString = string.Join(' ', scopes ?? []).Trim();
        return Task.FromResult(BuildAuthorizationUrl(codeChallenge, state, clientUniqueKey, scopeString));
    }

    protected override Task<TidalTokens> ExchangeCodeForTokensInternalAsync(string authorizationCode, string codeVerifier, string redirectUri)
    {
        return ExchangeCodeAsync(authorizationCode, codeVerifier);
    }

    protected override Task<TidalTokens> RefreshTokensInternalAsync(string refreshToken)
    {
        return RefreshTokensAsync(refreshToken);
    }

    protected override Task RevokeTokensInternalAsync(TidalTokens session)
    {
        return LogoutAsync();
    }

    protected override string ExtractRefreshToken(TidalTokens session)
    {
        return session?.RefreshToken ?? string.Empty;
    }

    protected override async Task CacheSessionAsync(TidalTokens session)
    {
        this._currentTokens = session;
        await SaveSessionAsync(session).ConfigureAwait(false);
    }

    protected override async Task ClearCachedSessionAsync()
    {
        this._currentTokens = null;
        await this._tokenStorage.ClearAsync().ConfigureAwait(false);
    }

    private Task SaveSessionAsync(TidalTokens session)
    {
        return this._tokenStorage.SaveAsync(new TokenEnvelope<TidalTokens>(session, session.ExpiresAt));
    }

    private async Task<TidalTokens?> LoadStoredSessionAsync()
    {
        TokenEnvelope<TidalTokens>? envelope = await this._tokenStorage.LoadAsync().ConfigureAwait(false);
        return envelope?.Session;
    }

    public async Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "OAuthExchange");
        _ = Guard.NotNullOrWhiteSpace(authCode, nameof(authCode));
        _ = Guard.NotNullOrWhiteSpace(codeVerifier, nameof(codeVerifier));

        string codeChallenge = this._pkceGenerator.CreateS256Challenge(codeVerifier);
        string clientUniqueKey = GenerateClientUniqueKey(codeChallenge);

        HttpRequestMessage request = BuildTokenExchangeRequest(authCode, codeVerifier, clientUniqueKey);
        (bool success, HttpResponseMessage response) = await SafeOperationExecutor.TryExecuteAsync<HttpResponseMessage>(() => this._httpClient.SendAsync(request)).ConfigureAwait(false);

        if (!success || response == null)
        {
            throw new InvalidOperationException("Failed to exchange authorization code");
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Detect consumed / expired auth code: Tidal returns 400 invalid_grant when the
            // code has already been used once or has naturally expired.  Surface a plain-English
            // message so the user knows exactly what to do, and signal the caller via a typed
            // exception so it can clear the cached RedirectUrl field.
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && IsInvalidGrant(errorContent))
            {
                throw new TidalInvalidGrantException(
                    "Authorization code is invalid or expired — paste a fresh redirect URL from a new Tidal browser login (the previous code has been used).");
            }

            throw new HttpRequestException($"Token exchange failed: {response.StatusCode} - {LogRedactor.Redact(errorContent)}");
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        TidalTokenResponse? tokenData = JsonSerializer.Deserialize<TidalTokenResponse>(content) ?? throw new InvalidOperationException("Failed to parse token response");
        this._currentTokens = MapToTidalTokens(tokenData);
        await SaveSessionAsync(this._currentTokens).ConfigureAwait(false);
        return this._currentTokens;
    }

    public async Task<TidalTokens> RefreshTokensAsync(string refreshToken)
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "OAuthRefresh");
        HttpRequestMessage request = BuildTokenRefreshRequest(refreshToken);
        HttpResponseMessage response = await this._httpClient.SendAsync(request).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"Token refresh failed: {response.StatusCode} - {LogRedactor.Redact(errorContent)}");
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        TidalTokenResponse? tokenData = JsonSerializer.Deserialize<TidalTokenResponse>(content) ?? throw new InvalidOperationException("Failed to parse refresh token response");
        this._currentTokens = MapToTidalTokens(tokenData);
        await SaveSessionAsync(this._currentTokens).ConfigureAwait(false);
        return this._currentTokens;
    }

    public async Task LogoutAsync()
    {
        this._currentTokens = null;
        await this._tokenStorage.ClearAsync().ConfigureAwait(false);
    }

    public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(callbackUrl))
            {
                return TidalCallbackResult.Failure("Callback URL is empty");
            }

            if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out Uri? uri))
            {
                return TidalCallbackResult.Failure("Invalid URL format");
            }

            if (!uri.Host.Equals("tidal.com", StringComparison.OrdinalIgnoreCase))
            {
                return TidalCallbackResult.Failure("Invalid callback domain");
            }

            System.Collections.Specialized.NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);

            string? error = queryParams.Get("error");
            if (!string.IsNullOrEmpty(error))
            {
                return TidalCallbackResult.Failure($"OAuth error: {error}");
            }

            string? authCode = queryParams.Get("code");
            if (string.IsNullOrEmpty(authCode))
            {
                return TidalCallbackResult.Failure("Authorization code not found in callback URL");
            }

            string? state = queryParams.Get("state");
            return string.IsNullOrEmpty(state)
                ? TidalCallbackResult.Failure("State parameter not found in callback URL")
                : TidalCallbackResult.Success(authCode, state);
        }
        catch (Exception ex)
        {
            return TidalCallbackResult.Failure($"Failed to parse callback URL: {ex.Message}");
        }
    }

    private string BuildAuthorizationUrl(string codeChallenge, string state, string clientUniqueKey, string scope)
    {
        Dictionary<string, string> parameters = new()
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

        if (!string.IsNullOrWhiteSpace(scope))
        {
            parameters["scope"] = scope;
        }

        string queryString = string.Join("&", parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{TidalConstants.LOGIN_BASE}?{queryString}";
    }

    private HttpRequestMessage BuildTokenExchangeRequest(string authCode, string codeVerifier, string clientUniqueKey)
    {
        HttpRequestMessage request = new(HttpMethod.Post, TidalConstants.AUTH_BASE);
        FormUrlEncodedContent formData = new(
        [
            new KeyValuePair<string, string>("code", authCode),
            new KeyValuePair<string, string>("client_id", TidalConstants.CLIENT_ID_PKCE),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("redirect_uri", TidalConstants.REDIRECT_URI),
            new KeyValuePair<string, string>("scope", TidalConstants.OAUTH_SCOPE),
            new KeyValuePair<string, string>("code_verifier", codeVerifier),
            new KeyValuePair<string, string>("client_unique_key", clientUniqueKey),
            new KeyValuePair<string, string>("client_secret", TidalConstants.CLIENT_SECRET_PKCE)
        ]);
        request.Content = formData;
        return request;
    }

    private HttpRequestMessage BuildTokenRefreshRequest(string refreshToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, TidalConstants.AUTH_BASE);
        FormUrlEncodedContent formData = new(
        [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", TidalConstants.CLIENT_ID_PKCE),
            new KeyValuePair<string, string>("client_secret", TidalConstants.CLIENT_SECRET_PKCE)
        ]);
        request.Content = formData;
        return request;
    }

    protected override string GenerateSecureState()
    {
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("/", "_").Replace("+", "-").Replace("=", "");
    }


    private static string GenerateClientUniqueKey(string codeChallenge)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeChallenge));
        byte[] truncated = new byte[16];
        Array.Copy(hash, truncated, truncated.Length);
        return Convert.ToHexString(truncated).ToLowerInvariant();
    }

    private static TidalTokens MapToTidalTokens(TidalTokenResponse response)
    {
        string sessionId = response.user?.sessionId ?? string.Empty;
        string countryCode = response.user?.countryCode ?? string.Empty;
        string userId = response.user?.userId.ToString() ?? string.Empty;

        // Tidal's token response does not always include the user/session block.
        // In those cases, required fields are present on the access token (JWT).
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = TryGetJwtStringClaim(response.access_token, claimName: "sid") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            countryCode = TryGetJwtStringClaim(response.access_token, claimName: "cc") ?? string.Empty;
        }

        countryCode = string.IsNullOrWhiteSpace(countryCode) ? "US" : countryCode.Trim().ToUpperInvariant();

        return new(
                AccessToken: response.access_token,
                RefreshToken: response.refresh_token,
                TokenType: response.token_type,
                ExpiresAt: DateTime.UtcNow.AddSeconds(response.expires_in),
                SessionId: sessionId,
                CountryCode: countryCode,
                UserId: userId);
    }

    public async Task<TidalTokens> GetValidTokensAsync()
    {
        if (this._currentTokens != null && !this._currentTokens.IsExpired)
        {
            return this._currentTokens;
        }

        TidalTokens? stored = await LoadStoredSessionAsync().ConfigureAwait(false);
        if (stored != null && !stored.IsExpired)
        {
            TidalTokens normalized = EnsureRequiredSessionFields(stored);
            if (string.IsNullOrWhiteSpace(normalized.SessionId) && !string.IsNullOrEmpty(stored.RefreshToken))
            {
                // Stored tokens are structurally incomplete for API calls; attempt a refresh even if not expired.
                normalized = EnsureRequiredSessionFields(await RefreshTokensAsync(stored.RefreshToken).ConfigureAwait(false));
            }

            if (string.IsNullOrWhiteSpace(normalized.SessionId))
            {
                throw new InvalidOperationException("Not authenticated (missing session identifier). Re-authenticate Tidalarr.");
            }

            if (!stored.Equals(normalized))
            {
                await SaveSessionAsync(normalized).ConfigureAwait(false);
            }

            this._currentTokens = normalized;
            return normalized;
        }

        if (stored != null && stored.IsExpired && !string.IsNullOrEmpty(stored.RefreshToken))
        {
            TidalTokens refreshed = await RefreshTokensAsync(stored.RefreshToken).ConfigureAwait(false);
            TidalTokens normalized = EnsureRequiredSessionFields(refreshed);
            if (string.IsNullOrWhiteSpace(normalized.SessionId))
            {
                throw new InvalidOperationException("Not authenticated (missing session identifier). Re-authenticate Tidalarr.");
            }

            if (!refreshed.Equals(normalized))
            {
                await SaveSessionAsync(normalized).ConfigureAwait(false);
            }

            this._currentTokens = normalized;
            return normalized;
        }

        throw new InvalidOperationException("Not authenticated");
    }

    // IStreamingTokenProvider implementation for shared OAuth handler
    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            TidalTokens tokens = await GetValidTokensAsync().ConfigureAwait(false);
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
            TidalTokens? stored = this._currentTokens ?? await LoadStoredSessionAsync().ConfigureAwait(false);
            if (stored == null || string.IsNullOrEmpty(stored.RefreshToken))
            {
                return string.Empty;
            }

            TidalTokens refreshed = await RefreshTokensAsync(stored.RefreshToken).ConfigureAwait(false);
            return refreshed.AccessToken;
        }
        catch
        {
            return string.Empty;
        }
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(false);
        }

        bool valid = this._currentTokens != null && !this._currentTokens.IsExpired && this._currentTokens.AccessToken == token;
        return Task.FromResult(valid);
    }

    public DateTime? GetTokenExpiration(string token)
    {
        return this._currentTokens != null && this._currentTokens.AccessToken == token ? this._currentTokens.ExpiresAt : null;
    }

    public void ClearAuthenticationCache()
    {
        this._currentTokens = null;
        try { _ = this._tokenStorage.ClearAsync(); } catch { /* ignore */ }
    }

    public new bool SupportsRefresh => true;
    public string ServiceName => "Tidal";

    private static TidalTokens EnsureRequiredSessionFields(TidalTokens tokens)
    {
        string sessionId = tokens.SessionId;
        string countryCode = tokens.CountryCode;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = TryGetJwtStringClaim(tokens.AccessToken, claimName: "sid") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            countryCode = TryGetJwtStringClaim(tokens.AccessToken, claimName: "cc") ?? string.Empty;
        }

        countryCode = string.IsNullOrWhiteSpace(countryCode) ? "US" : countryCode.Trim().ToUpperInvariant();

        return string.Equals(tokens.SessionId, sessionId, StringComparison.Ordinal) &&
            string.Equals(tokens.CountryCode, countryCode, StringComparison.Ordinal)
            ? tokens
            : (tokens with { SessionId = sessionId, CountryCode = countryCode });
    }

    private static string? TryGetJwtStringClaim(string? jwt, string claimName)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        string[] parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        string payloadJson;
        try
        {
            byte[] payloadBytes = Base64UrlDecode(parts[1]);
            payloadJson = Encoding.UTF8.GetString(payloadBytes);
        }
        catch
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(payloadJson);
            return !doc.RootElement.TryGetProperty(claimName, out JsonElement element)
                ? null
                : element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        string padded = base64Url.Replace('-', '+').Replace('_', '/');
        int mod = padded.Length % 4;
        if (mod == 2)
        {
            padded += "==";
        }
        else if (mod == 3)
        {
            padded += "=";
        }
        else if (mod != 0)
        {
            throw new FormatException("Invalid base64url length");
        }

        return Convert.FromBase64String(padded);
    }

    /// <summary>
    /// Returns true when a Tidal token-endpoint error body signals that the authorization
    /// code has already been consumed or has expired (<c>invalid_grant</c>).
    /// </summary>
    private static bool IsInvalidGrant(string errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(errorBody);
            return doc.RootElement.TryGetProperty("error", out JsonElement errorProp) &&
                   string.Equals(errorProp.GetString(), "invalid_grant", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Unparseable body — treat as a generic failure, not invalid_grant.
            return false;
        }
    }
}

/// <summary>
/// Thrown when Tidal rejects an authorization code exchange with <c>invalid_grant</c>,
/// meaning the code was already used or has expired.  Callers should clear any cached
/// redirect-URL / authorization-code state and prompt the user to start a fresh login.
/// </summary>
public sealed class TidalInvalidGrantException(string message) : Exception(message);

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
