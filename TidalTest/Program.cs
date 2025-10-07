using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Lidarr.Plugin.Common.Services.Authentication;

namespace TidalTest;

public class TidalTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; }
}

public static class TidalAuthHelper
{
    public static string GenerateClientUniqueKey()
    {
        return $"{BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0):x}";
    }
}

public static class UrlHelper
{
    public static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        
        if (string.IsNullOrEmpty(query))
            return result;
            
        // Remove leading '?' if present
        if (query.StartsWith("?"))
            query = query.Substring(1);
            
        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                var value = Uri.UnescapeDataString(parts[1]);
                result[key] = value;
            }
        }
        
        return result;
    }
}

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static string? currentCodeVerifier;
    private static string? currentClientUniqueKey;
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎵 Tidalarr End-to-End Test Suite");
        Console.WriteLine("==================================");
        Console.WriteLine();
        
        if (args.Length == 0)
        {
            await ShowMenu();
        }
        else
        {
            await ProcessCommand(args);
        }
    }
    
    static async Task ShowMenu()
    {
        while (true)
        {
            Console.WriteLine("Available Tests:");
            Console.WriteLine("1. auth-url     - Generate Tidal OAuth URL");
            Console.WriteLine("2. test-tokens  - Test token exchange (requires auth code)");
            Console.WriteLine("3. test-api     - Test Tidal API calls (requires tokens)");
            Console.WriteLine("4. test-search  - Test music search functionality");
            Console.WriteLine("5. test-download- Test track download functionality");
            Console.WriteLine("6. full-flow    - Complete authentication flow");
            Console.WriteLine("7. clear-tokens - Clear saved authentication");
            Console.WriteLine("8. exit         - Exit");
            Console.WriteLine();
            
            Console.Write("Select test (1-8): ");
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1" or "auth-url":
                    await GenerateAuthUrl();
                    break;
                case "2" or "test-tokens":
                    await TestTokenExchange();
                    break;
                case "3" or "test-api":
                    await TestTidalApi();
                    break;
                case "4" or "test-search":
                    await TestMusicSearch();
                    break;
                case "5" or "test-download":
                    await TestTrackDownload();
                    break;
                case "6" or "full-flow":
                    await FullAuthFlow();
                    break;
                case "7" or "clear-tokens":
                    await ClearTokens();
                    break;
                case "8" or "exit":
                    Console.WriteLine("👋 Goodbye!");
                    return;
                default:
                    Console.WriteLine("❌ Invalid choice. Please try again.");
                    break;
            }
            Console.WriteLine();
        }
    }
    
    static async Task ProcessCommand(string[] args)
    {
        var command = args[0].ToLower();
        
        switch (command)
        {
            case "auth-url":
                await GenerateAuthUrl();
                break;
            case "test-tokens":
                await TestTokenExchange();
                break;
            case "test-api":
                await TestTidalApi();
                break;
            case "test-search":
                await TestMusicSearch();
                break;
            case "test-download":
                await TestTrackDownload();
                break;
            case "full-flow":
                await FullAuthFlow();
                break;
            case "clear-tokens":
                await ClearTokens();
                break;
            default:
                Console.WriteLine($"❌ Unknown command: {command}");
                break;
        }
    }
    
    static async Task GenerateAuthUrl()
    {
        Console.WriteLine("🔐 Generating Tidal OAuth URL...");
        
        try
        {
            var pkceGenerator = new PKCEGenerator();
            var (codeVerifier, codeChallenge) = pkceGenerator.GeneratePair();
            var clientUniqueKey = TidalAuthHelper.GenerateClientUniqueKey();
            
            // Store for later use
            currentCodeVerifier = codeVerifier;
            currentClientUniqueKey = clientUniqueKey;
            
            var authUrl = BuildTidalAuthUrl(codeChallenge, clientUniqueKey);
            
            Console.WriteLine("✅ OAuth URL Generated Successfully!");
            Console.WriteLine();
            Console.WriteLine("📋 Next Steps:");
            Console.WriteLine("1. Open this URL in your browser:");
            Console.WriteLine($"   {authUrl}");
            Console.WriteLine();
            Console.WriteLine("2. Log into your Tidal account");
            Console.WriteLine("3. Copy the authorization code from the redirect URL");
            Console.WriteLine("4. Use the 'test-tokens' command with your auth code");
            Console.WriteLine();
            Console.WriteLine($"🔑 Your Code Verifier (save this!): {codeVerifier}");
            Console.WriteLine($"🔑 Your Client Unique Key (save this!): {clientUniqueKey}");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error generating auth URL: {ex.Message}");
        }
    }
    
    static async Task TestTokenExchange()
    {
        Console.WriteLine("🔄 Testing Token Exchange...");
        
        Console.WriteLine("📋 You can either:");
        Console.WriteLine("1. Paste the full redirect URL from Tidal");
        Console.WriteLine("2. Enter individual parameters manually");
        Console.WriteLine();
        
        Console.Write("Paste the redirect URL (or press ENTER for manual entry): ");
        var input = Console.ReadLine()?.Trim();
        
        string authCode, codeVerifier, clientUniqueKey;
        
        if (!string.IsNullOrEmpty(input) && input.StartsWith("https://"))
        {
            // Parse the redirect URL
            try
            {
                var uri = new Uri(input);
                var queryParams = UrlHelper.ParseQueryString(uri.Query);
                authCode = queryParams["code"];
                
                if (string.IsNullOrEmpty(authCode))
                {
                    Console.WriteLine("❌ No authorization code found in the URL.");
                    return;
                }
                
                Console.WriteLine($"✅ Extracted authorization code from URL");
                
                Console.Write("Enter your code verifier: ");
                codeVerifier = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(codeVerifier))
                {
                    Console.WriteLine("❌ Code verifier is required.");
                    return;
                }
                
                Console.Write("Enter your client unique key: ");
                clientUniqueKey = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(clientUniqueKey))
                {
                    Console.WriteLine("❌ Client unique key is required.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error parsing redirect URL: {ex.Message}");
                return;
            }
        }
        else
        {
            // Manual entry
            Console.Write("Enter your authorization code: ");
            authCode = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(authCode))
            {
                Console.WriteLine("❌ Authorization code is required.");
                return;
            }
            
            Console.Write("Enter your code verifier: ");
            codeVerifier = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(codeVerifier))
            {
                Console.WriteLine("❌ Code verifier is required.");
                return;
            }
            
            Console.Write("Enter your client unique key: ");
            clientUniqueKey = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(clientUniqueKey))
            {
                Console.WriteLine("❌ Client unique key is required.");
                return;
            }
        }
        
        try
        {
            var result = await ExchangeCodeForTokens(authCode, codeVerifier, clientUniqueKey);
            
            if (result.Success)
            {
                Console.WriteLine("✅ Token exchange successful!");
                Console.WriteLine("📄 Token Response:");
                Console.WriteLine(result.Data);
            }
            else
            {
                Console.WriteLine($"❌ Token exchange failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during token exchange: {ex.Message}");
        }
    }
    
    static async Task TestTidalApi()
    {
        Console.WriteLine("🌊 Testing Tidal API...");
        
        // Try to use saved tokens first
        var validTokens = await TokenStorage.GetValidTokensAsync();
        
        if (validTokens != null)
        {
            Console.WriteLine($"✅ Using saved authentication for {validTokens.Email}");
            
            try
            {
                var result = await TestTidalApiCall(validTokens.AccessToken);
                
                if (result.Success)
                {
                    Console.WriteLine("✅ Tidal API test successful!");
                    Console.WriteLine("📄 API Response:");
                    Console.WriteLine(result.Data);
                }
                else
                {
                    Console.WriteLine($"❌ Tidal API test failed: {result.Message}");
                }
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing Tidal API: {ex.Message}");
                return;
            }
        }
        
        Console.WriteLine("❌ No valid authentication found.");
        Console.WriteLine("💡 Run 'dotnet run full-flow' first to authenticate.");
    }
    
    static async Task FullAuthFlow()
    {
        Console.WriteLine("🚀 Starting Full Authentication Flow...");
        Console.WriteLine();
        
        // Step 0: Check for existing valid tokens (with automatic refresh)
        Console.WriteLine("📋 Step 0: Checking for existing authentication...");
        var validTokens = await TokenStorage.GetValidTokensAsync();
        
        if (validTokens != null)
        {
            Console.WriteLine("✅ Found valid authentication!");
            Console.WriteLine($"👤 User: {validTokens.Email}");
            Console.WriteLine($"🌍 Country: {validTokens.CountryCode}");
            Console.WriteLine($"⏰ Expires: {validTokens.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();
            
            Console.Write("Use existing authentication? (Y/n): ");
            var useExisting = Console.ReadLine()?.Trim().ToLower();
            
            if (useExisting != "n" && useExisting != "no")
            {
                Console.WriteLine("🚀 Testing API with authentication...");
                var apiResult = await TestTidalApiCall(validTokens.AccessToken);
                
                if (apiResult.Success)
                {
                    Console.WriteLine("✅ Authentication works perfectly!");
                    Console.WriteLine("📄 API Response:");
                    Console.WriteLine(apiResult.Data);
                    Console.WriteLine();
                    Console.WriteLine("🏆 Authentication flow completed - no re-auth needed!");
                    return;
                }
                else
                {
                    Console.WriteLine("❌ Authentication failed, proceeding with fresh login...");
                    await TokenStorage.ClearTokensAsync();
                }
            }
            else
            {
                Console.WriteLine("🔄 Proceeding with fresh authentication as requested...");
                await TokenStorage.ClearTokensAsync();
            }
        }
        else
        {
            Console.WriteLine("🆕 No valid authentication found");
        }
        
        Console.WriteLine();
        
        // Step 1: Generate Auth URL
        Console.WriteLine("📋 Step 1: Generating OAuth URL");
        await GenerateAuthUrl();
        
        Console.WriteLine();
        Console.WriteLine("⏸️  MANUAL STEP REQUIRED:");
        Console.WriteLine("   1. Open the URL above in your browser");
        Console.WriteLine("   2. Complete Tidal authentication");
        Console.WriteLine("   3. Copy the complete redirect URL from your browser");
        Console.WriteLine();
        Console.Write("Paste the complete redirect URL here: ");
        var redirectUrl = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(redirectUrl) || !redirectUrl.StartsWith("https://"))
        {
            Console.WriteLine("❌ Invalid redirect URL provided.");
            return;
        }
        
        // Step 2: Parse and exchange tokens
        try
        {
            var uri = new Uri(redirectUrl);
            var queryParams = UrlHelper.ParseQueryString(uri.Query);
            var authCode = queryParams["code"];
            
            if (string.IsNullOrEmpty(authCode))
            {
                Console.WriteLine("❌ No authorization code found in the redirect URL.");
                return;
            }
            
            Console.WriteLine("✅ Successfully extracted authorization code from redirect URL");
            
            // Use the stored values from the auth URL generation
            if (string.IsNullOrEmpty(currentCodeVerifier) || string.IsNullOrEmpty(currentClientUniqueKey))
            {
                Console.WriteLine("❌ Missing authentication parameters. Please run 'auth-url' first.");
                return;
            }
            
            Console.WriteLine($"✅ Using stored authentication parameters");
            var codeVerifier = currentCodeVerifier;
            var clientUniqueKey = currentClientUniqueKey;
            
            var result = await ExchangeCodeForTokens(authCode, codeVerifier, clientUniqueKey);
            
            if (result.Success)
            {
                Console.WriteLine("✅ Token exchange successful!");
                Console.WriteLine("📄 Token Response:");
                Console.WriteLine(result.Data);
                
                // Parse, save, and test tokens automatically
                try
                {
                    var tokenInfo = TokenStorage.ParseTokenResponse(result.Data);
                    await TokenStorage.SaveTokensAsync(tokenInfo);
                    
                    Console.WriteLine();
                    Console.WriteLine($"👤 Authenticated as: {tokenInfo.Email}");
                    Console.WriteLine($"🌍 Country: {tokenInfo.CountryCode}");
                    Console.WriteLine($"⏰ Token expires: {tokenInfo.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
                    
                    Console.WriteLine();
                    Console.WriteLine("🚀 Testing API automatically with new tokens...");
                    var apiResult = await TestTidalApiCall(tokenInfo.AccessToken);
                    
                    if (apiResult.Success)
                    {
                        Console.WriteLine("✅ Tidal API test successful!");
                        Console.WriteLine("📄 API Response:");
                        Console.WriteLine(apiResult.Data);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Tidal API test failed: {apiResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Could not parse token response: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Token exchange failed: {result.Message}");
                Console.WriteLine("📄 Error Response:");
                Console.WriteLine(result.Data);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing redirect URL: {ex.Message}");
            return;
        }
        
        // API test is now automatic - no need for manual prompt
        
        Console.WriteLine();
        Console.WriteLine("🏆 Full authentication flow test completed!");
    }
    
    static async Task ClearTokens()
    {
        Console.WriteLine("🗑️ Clearing saved authentication...");
        await TokenStorage.ClearTokensAsync();
        Console.WriteLine("✅ Authentication cleared. Next authentication will require fresh login.");
    }
    
    private static string BuildTidalAuthUrl(string codeChallenge, string clientUniqueKey)
    {
        var clientId = "6BDSRdpK9hqEBTgU";
        var redirectUri = "https://tidal.com/android/login/auth";
        
        return $"https://login.tidal.com/authorize" +
               $"?response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&client_id={clientId}" +
               $"&lang=EN" +
               $"&appMode=android" +
               $"&client_unique_key={clientUniqueKey}" +
               $"&code_challenge={codeChallenge}" +
               $"&code_challenge_method=S256" +
               $"&restrict_signup=true";
    }
    
    private static async Task<TidalTestResult> ExchangeCodeForTokens(string authCode, string codeVerifier, string clientUniqueKey)
    {
        var tokenUrl = "https://auth.tidal.com/v1/oauth2/token";
        var clientId = "6BDSRdpK9hqEBTgU";
        var redirectUri = "https://tidal.com/android/login/auth";
        
        var requestData = new Dictionary<string, string>
        {
            ["code"] = authCode,
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = "r_usr+w_usr+w_sub",
            ["code_verifier"] = codeVerifier,
            ["client_unique_key"] = clientUniqueKey
        };
        
        var formData = new FormUrlEncodedContent(requestData);
        
        try
        {
            var response = await httpClient.PostAsync(tokenUrl, formData);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return new TidalTestResult
                {
                    Success = true,
                    Message = "Token exchange successful",
                    Data = responseContent
                };
            }
            else
            {
                return new TidalTestResult
                {
                    Success = false,
                    Message = $"HTTP {response.StatusCode}: {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            return new TidalTestResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
    
    private static async Task<TidalTestResult> TestTidalApiCall(string accessToken)
    {
        var apiUrl = "https://api.tidal.com/v1/sessions";
        
        try
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            
            var response = await httpClient.GetAsync(apiUrl);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return new TidalTestResult
                {
                    Success = true,
                    Message = "API call successful",
                    Data = responseContent
                };
            }
            else
            {
                return new TidalTestResult
                {
                    Success = false,
                    Message = $"HTTP {response.StatusCode}: {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            return new TidalTestResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
