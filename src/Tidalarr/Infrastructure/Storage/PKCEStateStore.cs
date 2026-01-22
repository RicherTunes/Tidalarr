using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Constants;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// Persists PKCE state (code_verifier, state, clientUniqueKey) between OAuth authorization and token exchange.
/// Required because the PKCE flow requires the original code_verifier when exchanging the authorization code.
/// </summary>
public class PKCEStateStore
{
    private readonly string _configPath;
    private readonly string _storagePath;
    private const int StateTtlMinutes = 30;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    // In-memory cache for PKCE state, keyed by normalized config path.
    // This allows URL generation without file I/O during schema rendering.
    private static readonly ConcurrentDictionary<string, PKCEState> InMemoryCache = new();

    public static bool IsCallbackStateMatch(PKCEState storedState, string callbackState)
    {
        if (storedState is null) throw new ArgumentNullException(nameof(storedState));
        return !string.IsNullOrWhiteSpace(callbackState) &&
               string.Equals(storedState.State, callbackState, StringComparison.Ordinal);
    }

    public static string? TryReadAuthorizationUrl(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return null;
        }

        try
        {
            string storagePath = Path.Combine(configPath, "pkce_state.json");
            if (!File.Exists(storagePath))
            {
                return null;
            }

            string json = File.ReadAllText(storagePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            return !document.RootElement.TryGetProperty("authorizationUrl", out JsonElement authorizationUrlElement)
                ? null
                : authorizationUrlElement.GetString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns an authorization URL, generating one in-memory if needed.
    /// This method never requires file I/O and is safe to call during schema rendering.
    /// </summary>
    /// <remarks>
    /// Uses an in-memory cache keyed by configPath (similar to TrevTV's singleton pattern).
    /// The state is regenerated if expired. File persistence happens separately during token exchange.
    /// </remarks>
    public static string? TryGetOrCreateAuthorizationUrl(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return null;
        }

        try
        {
            string cacheKey = configPath.ToLowerInvariant();

            // Check in-memory cache first (fast path, no I/O)
            if (InMemoryCache.TryGetValue(cacheKey, out PKCEState? cachedState) &&
                cachedState.CreatedAt.AddMinutes(StateTtlMinutes) >= DateTime.UtcNow)
            {
                return cachedState.AuthorizationUrl;
            }

            // Try to load from file if exists (for state continuity across restarts)
            string storagePath = Path.Combine(configPath, "pkce_state.json");
            PKCEState? fileState = TryReadState(storagePath);
            if (fileState != null && fileState.CreatedAt.AddMinutes(StateTtlMinutes) >= DateTime.UtcNow)
            {
                InMemoryCache[cacheKey] = fileState;
                return fileState.AuthorizationUrl;
            }

            // Generate new state in-memory (no file I/O required)
            PKCEState newState = CreateState();
            InMemoryCache[cacheKey] = newState;

            // Attempt to persist to file (best-effort, don't fail if directory doesn't exist)
            TryPersistState(configPath, storagePath, newState);

            return newState.AuthorizationUrl;
        }
        catch
        {
            // Last resort: generate URL without caching
            try
            {
                return CreateState().AuthorizationUrl;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Attempts to persist state to file. Best-effort, never throws.
    /// </summary>
    private static void TryPersistState(string configPath, string storagePath, PKCEState state)
    {
        try
        {
            if (!Directory.Exists(configPath))
            {
                Directory.CreateDirectory(configPath);
            }

            string json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(storagePath, json);
        }
        catch
        {
            // Silently ignore persistence failures - URL generation still works
        }
    }

    /// <summary>
    /// Regenerates PKCE codes for a given config path. Call after successful token exchange.
    /// </summary>
    public static void RegenerateCodes(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return;
        }

        string cacheKey = configPath.ToLowerInvariant();
        PKCEState newState = CreateState();
        InMemoryCache[cacheKey] = newState;

        string storagePath = Path.Combine(configPath, "pkce_state.json");
        TryPersistState(configPath, storagePath, newState);
    }

    public PKCEStateStore(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentNullException(nameof(configPath), "Config path is required for PKCE state storage");

        this._configPath = configPath;
        this._storagePath = Path.Combine(configPath, "pkce_state.json");
        EnsureStorageDirectoryExists();
    }

    public async Task SaveStateAsync(PKCEState state)
    {
        try
        {
            // Update in-memory cache
            string cacheKey = this._configPath.ToLowerInvariant();
            InMemoryCache[cacheKey] = state;

            // Persist to disk
            string json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(this._storagePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save PKCE state: {ex.Message}", ex);
        }
    }

    public async Task<PKCEState?> LoadStateAsync()
    {
        try
        {
            string cacheKey = this._configPath.ToLowerInvariant();

            // Check in-memory cache first (fast path, critical for schema->validation flow)
            if (InMemoryCache.TryGetValue(cacheKey, out PKCEState? cachedState))
            {
                // Check if state has expired
                if (cachedState.CreatedAt.AddMinutes(StateTtlMinutes) >= DateTime.UtcNow)
                {
                    return cachedState;
                }
                // Expired - remove from cache
                InMemoryCache.TryRemove(cacheKey, out _);
            }

            // Fall back to disk
            if (!File.Exists(this._storagePath))
                return null;

            string json = await File.ReadAllTextAsync(this._storagePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            PKCEState? state = JsonSerializer.Deserialize<PKCEState>(json, JsonOptions);

            // Check if state has expired
            if (state != null && state.CreatedAt.AddMinutes(StateTtlMinutes) < DateTime.UtcNow)
            {
                await DeleteStateAsync();
                return null;
            }

            // Update in-memory cache with loaded state
            if (state != null)
            {
                InMemoryCache[cacheKey] = state;
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    public Task DeleteStateAsync()
    {
        try
        {
            // Remove from in-memory cache
            string cacheKey = this._configPath.ToLowerInvariant();
            InMemoryCache.TryRemove(cacheKey, out _);

            // Delete from disk
            if (File.Exists(this._storagePath))
                File.Delete(this._storagePath);
        }
        catch
        {
            // Swallow deletion errors
        }
        return Task.CompletedTask;
    }

    private void EnsureStorageDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(this._storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }

    private static PKCEState? TryReadState(string storagePath)
    {
        try
        {
            if (!File.Exists(storagePath))
            {
                return null;
            }

            string json = File.ReadAllText(storagePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PKCEState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static PKCEState CreateState()
    {
        string codeVerifier = GenerateBase64UrlString(byteLength: 32);
        string codeChallenge = CreateS256Challenge(codeVerifier);
        string state = GenerateBase64UrlString(byteLength: 32);
        string clientUniqueKey = GenerateClientUniqueKey(codeChallenge);
        string authorizationUrl = BuildAuthorizationUrl(codeChallenge, state, clientUniqueKey, TidalConstants.OAUTH_SCOPE);
        return new PKCEState(authorizationUrl, codeVerifier, state, clientUniqueKey, DateTime.UtcNow);
    }

    private static string GenerateBase64UrlString(int byteLength)
    {
        byte[] bytes = new byte[byteLength];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);
    }

    private static string CreateS256Challenge(string codeVerifier)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(challengeBytes)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);
    }

    private static string GenerateClientUniqueKey(string codeChallenge)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeChallenge));
        byte[] truncated = new byte[16];
        Array.Copy(hash, truncated, truncated.Length);
        return Convert.ToHexString(truncated).ToLowerInvariant();
    }

    private static string BuildAuthorizationUrl(string codeChallenge, string state, string clientUniqueKey, string scope)
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

        string queryString = string.Join("&", parameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{TidalConstants.LOGIN_BASE}?{queryString}";
    }
}

/// <summary>
/// PKCE state data persisted between authorization URL generation and token exchange.
/// </summary>
public record PKCEState(
    string AuthorizationUrl,
    string CodeVerifier,
    string State,
    string ClientUniqueKey,
    DateTime CreatedAt);
