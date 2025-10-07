using Tidalarr.Integration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using TidalQuality = Tidalarr.Core.Models.TidalQuality;

namespace TidalCLI;

public class PKCEGenerator
{
    public (string codeVerifier, string codeChallenge) GenerateChallenge()
    {
        // Generate random 32-byte code verifier
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var codeVerifier = Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Create SHA256 hash of code verifier for challenge
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Convert.ToBase64String(challengeBytes)
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

public class TidalOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly PKCEGenerator _pkceGenerator;

    public TidalOAuthService(HttpClient httpClient, PKCEGenerator pkceGenerator)
    {
        _httpClient = httpClient;
        _pkceGenerator = pkceGenerator;
    }

    public Task<TidalOAuthUrl> GenerateAuthUrlAsync()
    {
        var (codeVerifier, codeChallenge) = _pkceGenerator.GenerateChallenge();
        var state = Guid.NewGuid().ToString("N");

        var clientId = "6BDSRdpK9hqEBTgU";
        var redirectUri = "https://tidal.com/android/login/auth";
        var clientUniqueKey = Guid.NewGuid().ToString("N");

        var authUrl = "https://login.tidal.com/authorize?" +
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
            var uri = new Uri(callbackUrl);
            var query = HttpUtility.ParseQueryString(uri.Query);

            if (query["error"] != null)
            {
                return new TidalCallbackResult
                {
                    IsSuccess = false,
                    ErrorMessage = query["error"] ?? string.Empty
                };
            }

            var code = query["code"];
            string? state = query["state"];

            if (string.IsNullOrEmpty(code))
            {
                return new TidalCallbackResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No authorization code found"
                };
            }

            return new TidalCallbackResult
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









