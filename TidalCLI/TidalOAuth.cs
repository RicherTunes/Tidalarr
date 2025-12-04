using Tidalarr.Integration;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using TidalQuality = Tidalarr.Core.Models.TidalQuality;

namespace TidalCLI;

public class PKCEGenerator
{
    public (string codeVerifier, string codeChallenge) GenerateChallenge()
    {
        // Generate random 32-byte code verifier
        byte[] randomBytes = new byte[32];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        string codeVerifier = Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Create SHA256 hash of code verifier for challenge
        using SHA256 sha256 = SHA256.Create();
        byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        string codeChallenge = Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (codeVerifier, codeChallenge);
    }
}

public class TidalOAuthUrl
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class TidalCallbackResult
{
    public bool IsSuccess { get; set; }
    public string AuthCode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class TidalOAuthService(HttpClient httpClient, PKCEGenerator pkceGenerator)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly PKCEGenerator _pkceGenerator = pkceGenerator;

    public Task<TidalOAuthUrl> GenerateAuthUrlAsync()
    {
        (string codeVerifier, string codeChallenge) = this._pkceGenerator.GenerateChallenge();
        string state = Guid.NewGuid().ToString("N");

        string clientId = "6BDSRdpK9hqEBTgU";
        string redirectUri = "https://tidal.com/android/login/auth";
        string clientUniqueKey = Guid.NewGuid().ToString("N");

        string authUrl = "https://login.tidal.com/authorize?" +
            $"client_id={clientId}&" +
            $"response_type=code&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"scope=r_usr+w_usr&" +
            $"state={state}&" +
            $"code_challenge={codeChallenge}&" +
            $"code_challenge_method=S256&" +
            $"client_unique_key={clientUniqueKey}";

        return Task.FromResult(new TidalOAuthUrl
        {
            AuthorizationUrl = authUrl,
            CodeVerifier = codeVerifier,
            State = state
        });
    }

    public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
    {
        try
        {
            Uri uri = new Uri(callbackUrl);
            System.Collections.Specialized.NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

            if (query["error"] != null)
            {
                return new TidalCallbackResult
                {
                    IsSuccess = false,
                    ErrorMessage = query["error"] ?? string.Empty
                };
            }

            string? code = query["code"];
            string? state = query["state"];

            return string.IsNullOrEmpty(code)
                ? new TidalCallbackResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No authorization code found"
                }
                : new TidalCallbackResult
            {
                IsSuccess = true,
                AuthCode = code,
                State = state ?? ""
            };
        }
        catch (Exception ex)
        {
            return new TidalCallbackResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}



// Simple mock download client for testing
public class MockDownloadClient
{
    public async Task<bool> ValidateDownloadAsync(string trackId, TidalQuality quality)
    {
        // Mock validation - always return true for testing
        await Task.Delay(100); // Simulate async work
        return true;
    }
}

// Simple module implementation for testing
public static class TidalMockModule
{
    public static object CreateIndexer(object logger, TidalarrSettings settings)
    {
        Console.WriteLine("✅ Tidal indexer created successfully");
        return new object();
    }

    public static MockDownloadClient CreateDownloadClient(object logger, TidalarrSettings settings)
    {
        Console.WriteLine("✅ Tidal download client created successfully");
        return new MockDownloadClient();
    }

    public static bool ValidateConfiguration(TidalarrSettings settings)
    {
        return settings.IsValid(out _);
    }
}









