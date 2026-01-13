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
    private readonly string _storagePath;
    private const int StateTtlMinutes = 30;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

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
    /// Returns the persisted authorization URL if present and unexpired, otherwise generates a new PKCE state,
    /// persists it to <c>pkce_state.json</c>, and returns the new authorization URL.
    /// </summary>
    /// <remarks>
    /// Used by UI schema rendering; must not throw. Returns <c>null</c> on IO/parse errors.
    /// </remarks>
    public static string? TryGetOrCreateAuthorizationUrl(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return null;
        }

        try
        {
            string storagePath = Path.Combine(configPath, "pkce_state.json");

            PKCEState? existingState = TryReadState(storagePath);
            if (existingState != null && existingState.CreatedAt.AddMinutes(StateTtlMinutes) >= DateTime.UtcNow)
            {
                return existingState.AuthorizationUrl;
            }

            if (!Directory.Exists(configPath))
            {
                _ = Directory.CreateDirectory(configPath);
            }

            PKCEState newState = CreateState();
            string json = JsonSerializer.Serialize(newState, JsonOptions);
            File.WriteAllText(storagePath, json);
            return newState.AuthorizationUrl;
        }
        catch
        {
            return null;
        }
    }

    public PKCEStateStore(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentNullException(nameof(configPath), "Config path is required for PKCE state storage");

        this._storagePath = Path.Combine(configPath, "pkce_state.json");
        EnsureStorageDirectoryExists();
    }

    public async Task SaveStateAsync(PKCEState state)
    {
        try
        {
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
            if (!File.Exists(this._storagePath))
                return null;

            string json = await File.ReadAllTextAsync(this._storagePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            PKCEState? state = JsonSerializer.Deserialize<PKCEState>(json, JsonOptions);

            // Check if state has expired (10 minutes is typical for PKCE)
            if (state != null && state.CreatedAt.AddMinutes(StateTtlMinutes) < DateTime.UtcNow)
            {
                await DeleteStateAsync();
                return null;
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
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PKCEState>(json, JsonOptions);
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
