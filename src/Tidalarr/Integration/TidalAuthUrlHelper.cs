using System.Security.Cryptography;
using System.Text;
using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Constants;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration;

/// <summary>
/// Static helper for generating OAuth authorization URLs.
/// Persists PKCE state under ConfigPath so OAuth can resume across restarts.
/// </summary>
public static class TidalAuthUrlHelper
{
    private static readonly TimeSpan MaxPkceAge = TimeSpan.FromHours(1);
    private static readonly object Sync = new();

    public static string GetDefaultConfigPath()
    {
        try
        {
            if (OperatingSystem.IsLinux() && Directory.Exists("/config"))
                return "/config/tidalarr";
        }
        catch
        {
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
            return Path.Combine(appData, "Tidalarr");

        string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(commonAppData))
            return Path.Combine(commonAppData, "Tidalarr");

        return Path.Combine(Path.GetTempPath(), "Tidalarr");
    }

    /// <summary>
    /// Gets the current authorization URL, generating a new one if needed.
    /// The URL and associated PKCE parameters are persisted under ConfigPath for
    /// the auth code exchange.
    /// </summary>
    public static string GetAuthorizationUrl(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            return string.Empty;

        try
        {
            lock (Sync)
            {
                var store = new PkceStateFileStore(configPath);
                PkceState? existing = store.TryLoad();
                DateTimeOffset now = DateTimeOffset.UtcNow;

                if (existing is not null &&
                    !existing.IsExpired(MaxPkceAge, now) &&
                    !string.IsNullOrWhiteSpace(existing.AuthorizationUrl) &&
                    !string.IsNullOrWhiteSpace(existing.CodeVerifier) &&
                    !string.IsNullOrWhiteSpace(existing.State))
                    return existing.AuthorizationUrl;

                PkceState created = CreateNewPkceState(now);
                store.Save(created);
                return created.AuthorizationUrl;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static PkceState CreateNewPkceState(DateTimeOffset nowUtc)
    {
        PKCEGenerator pkce = new();
        (string verifier, string challenge) = pkce.GeneratePair();

        string state = GenerateSecureState();
        string clientUniqueKey = GenerateClientUniqueKey(challenge);
        string url = BuildAuthorizationUrl(challenge, state, clientUniqueKey);

        return new PkceState(verifier, state, nowUtc, url);
    }

    private static string GenerateSecureState()
    {
        byte[] bytes = new byte[32];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
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

    private static string BuildAuthorizationUrl(string codeChallenge, string state, string clientUniqueKey)
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

        string queryString = string.Join("&", parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{TidalConstants.LOGIN_BASE}?{queryString}";
    }
}
