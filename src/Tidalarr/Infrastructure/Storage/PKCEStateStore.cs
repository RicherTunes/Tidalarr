using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Constants;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// Persists PKCE state (code_verifier, state, clientUniqueKey) between OAuth authorization and token exchange.
/// Required because the PKCE flow requires the original code_verifier when exchanging the authorization code.
/// </summary>
/// <remarks>
/// Persistence routes through <see cref="FileTokenStore{TSession}"/>, which encrypts the payload at rest
/// using the platform token protector (DPAPI on Windows, Keychain on macOS, libsecret/DataProtection on Linux).
/// The legacy plaintext format is auto-migrated on first read.
/// </remarks>
public class PKCEStateStore
{
    private const string StateFileName = "pkce_state.json";
    private const int StateTtlMinutes = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    // In-memory cache for PKCE state, keyed by normalized config path.
    // This allows URL generation without file I/O during schema rendering.
    private static readonly ConcurrentDictionary<string, PKCEState> InMemoryCache = new();

    private readonly string _configPath;
    private readonly string _storagePath;
    private readonly FileTokenStore<PKCEState> _store;

    public PKCEStateStore(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentNullException(nameof(configPath), "Config path is required for PKCE state storage");
        }

        this._configPath = configPath;
        this._storagePath = Path.Combine(configPath, StateFileName);
        EnsureStorageDirectoryExists();
        this._store = new FileTokenStore<PKCEState>(this._storagePath);
    }

    public static bool IsCallbackStateMatch(PKCEState storedState, string callbackState)
    {
        return storedState is null
            ? throw new ArgumentNullException(nameof(storedState))
            : !string.IsNullOrWhiteSpace(callbackState) &&
               string.Equals(storedState.State, callbackState, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the cached authorization URL for the given config path without generating a new one.
    /// Returns null if no state exists or the state cannot be loaded.
    /// </summary>
    public static string? TryReadAuthorizationUrl(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return null;
        }

        try
        {
            string storagePath = Path.Combine(configPath, StateFileName);
            if (!File.Exists(storagePath))
            {
                return null;
            }

            PKCEState? state = LoadStateFromDisk(storagePath);
            return state?.AuthorizationUrl;
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
            string storagePath = Path.Combine(configPath, StateFileName);
            PKCEState? fileState = LoadStateFromDisk(storagePath);
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

        string storagePath = Path.Combine(configPath, StateFileName);
        TryPersistState(configPath, storagePath, newState);
    }

    public async Task SaveStateAsync(PKCEState state)
    {
        try
        {
            // Update in-memory cache
            string cacheKey = this._configPath.ToLowerInvariant();
            InMemoryCache[cacheKey] = state;

            // Persist to disk via encrypted token store
            await this._store.SaveAsync(new TokenEnvelope<PKCEState>(state, expiresAt: state.CreatedAt.AddMinutes(StateTtlMinutes)))
                .ConfigureAwait(false);
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
                _ = InMemoryCache.TryRemove(cacheKey, out _);
            }

            // Fall back to disk
            if (!File.Exists(this._storagePath))
            {
                return null;
            }

            PKCEState? state = LoadStateFromDisk(this._storagePath);

            // Check if state has expired
            if (state != null && state.CreatedAt.AddMinutes(StateTtlMinutes) < DateTime.UtcNow)
            {
                await DeleteStateAsync().ConfigureAwait(false);
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

    public async Task DeleteStateAsync()
    {
        try
        {
            // Remove from in-memory cache
            string cacheKey = this._configPath.ToLowerInvariant();
            _ = InMemoryCache.TryRemove(cacheKey, out _);

            // Delete from disk via the store (handles cross-process locking)
            await this._store.ClearAsync().ConfigureAwait(false);
        }
        catch
        {
            // Swallow deletion errors
        }
    }

    private void EnsureStorageDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(this._storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Loads state from disk, transparently handling both encrypted (v=2) and legacy plaintext formats.
    /// </summary>
    private static PKCEState? LoadStateFromDisk(string storagePath)
    {
        try
        {
            if (!File.Exists(storagePath))
            {
                return null;
            }

            string raw = File.ReadAllText(storagePath);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            // Detect format: v=2 protected envelope vs legacy plaintext PKCEState.
            using JsonDocument document = JsonDocument.Parse(raw);
            JsonElement root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("v", out JsonElement vElement) &&
                vElement.ValueKind == JsonValueKind.Number &&
                vElement.GetInt32() == 2)
            {
                // Encrypted envelope — load through the store, which knows how to decrypt.
                FileTokenStore<PKCEState> reader = new(storagePath);
                TokenEnvelope<PKCEState>? envelope = reader.LoadAsync().GetAwaiter().GetResult();
                return envelope?.Session;
            }

            // Legacy plaintext format: bare PKCEState fields at root.
            PKCEState? legacy = JsonSerializer.Deserialize<PKCEState>(raw, JsonOptions);
            if (legacy == null)
            {
                return null;
            }

            // Best-effort migration to encrypted format on next save (caller will trigger).
            // We also proactively migrate here so legacy plaintext is replaced ASAP.
            try
            {
                FileTokenStore<PKCEState> migrator = new(storagePath);
                migrator.SaveAsync(new TokenEnvelope<PKCEState>(legacy, expiresAt: legacy.CreatedAt.AddMinutes(StateTtlMinutes)))
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // Migration is best-effort; legacy file remains usable on this read.
            }

            return legacy;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to persist state to file via the encrypted token store. Best-effort, never throws.
    /// </summary>
    private static void TryPersistState(string configPath, string storagePath, PKCEState state)
    {
        try
        {
            if (!Directory.Exists(configPath))
            {
                _ = Directory.CreateDirectory(configPath);
            }

            FileTokenStore<PKCEState> store = new(storagePath);
            store.SaveAsync(new TokenEnvelope<PKCEState>(state, expiresAt: state.CreatedAt.AddMinutes(StateTtlMinutes)))
                .GetAwaiter().GetResult();
        }
        catch
        {
            // Silently ignore persistence failures - URL generation still works
        }
    }

    // PKCE crypto routes through Lidarr.Plugin.Common.Services.Authentication.PKCEGenerator (RFC 7636).
    // Single source of truth shared with TidalOAuthService.
    private static readonly IPKCEGenerator PkceGenerator = new PKCEGenerator();

    private static PKCEState CreateState()
    {
        (string codeVerifier, string codeChallenge) = PkceGenerator.GeneratePair();
        string state = GenerateBase64UrlState();
        string clientUniqueKey = GenerateClientUniqueKey(codeChallenge);
        string authorizationUrl = BuildAuthorizationUrl(codeChallenge, state, clientUniqueKey, TidalConstants.OAUTH_SCOPE);
        return new PKCEState(authorizationUrl, codeVerifier, state, clientUniqueKey, DateTime.UtcNow);
    }

    // CSRF state token — common's PKCEGenerator covers code_verifier/challenge but does not expose a
    // generic random-state helper, so this stays plugin-local. Matches TidalOAuthService.GenerateSecureState.
    private static string GenerateBase64UrlState()
    {
        byte[] bytes = new byte[32];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);
    }

    // Tidal-specific: clientUniqueKey is SHA-256(codeChallenge) truncated to 16 bytes hex.
    // Stays plugin-local because no equivalent exists in common (it's not standard OAuth/PKCE).
    private static string GenerateClientUniqueKey(string codeChallenge)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeChallenge));
        byte[] truncated = new byte[16];
        Array.Copy(hash, truncated, truncated.Length);
        return Convert.ToHexString(truncated).ToLowerInvariant();
    }

    // Tidal-specific authorize URL with client_unique_key/appMode query params — stays plugin-local.
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
