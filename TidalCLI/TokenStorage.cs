using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TidalCLI;

public class TidalTokenInfo
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired;
    public bool NeedsRefresh => DateTime.UtcNow >= ExpiresAt.AddMinutes(-15); // Refresh 15 min before expiry
}

public static class TokenStorage
{
    private static readonly string TokenFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "Tidalarr", 
        "test_tokens.json"
    );
    
    public static async Task<TidalTokenInfo?> LoadTokensAsync()
    {
        try
        {
            if (!File.Exists(TokenFilePath))
                return null;
                
            var json = await File.ReadAllTextAsync(TokenFilePath);
            return JsonSerializer.Deserialize<TidalTokenInfo>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error loading saved tokens: {ex.Message}");
            return null;
        }
    }
    
    public static async Task SaveTokensAsync(TidalTokenInfo tokenInfo)
    {
        try
        {
            var directory = Path.GetDirectoryName(TokenFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory!);
                
            var json = JsonSerializer.Serialize(tokenInfo, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(TokenFilePath, json);
            Console.WriteLine($"💾 Tokens saved to: {TokenFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error saving tokens: {ex.Message}");
        }
    }
    
    public static async Task ClearTokensAsync()
    {
        try
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
                Console.WriteLine("🗑️ Saved tokens cleared");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error clearing tokens: {ex.Message}");
        }
    }
    
    public static async Task<TidalTokenInfo?> RefreshTokensAsync(TidalTokenInfo currentTokens)
    {
        try
        {
            using var httpClient = new HttpClient();
            var tokenUrl = "https://auth.tidal.com/v1/oauth2/token";
            var clientId = "6BDSRdpK9hqEBTgU";
            var clientSecret = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
            
            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = currentTokens.RefreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            };
            
            var formData = new FormUrlEncodedContent(requestData);
            var response = await httpClient.PostAsync(tokenUrl, formData);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("🔄 Tokens refreshed successfully");
                return ParseTokenResponse(responseContent);
            }
            else
            {
                Console.WriteLine($"❌ Token refresh failed: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error refreshing tokens: {ex.Message}");
            return null;
        }
    }
    
    public static async Task<TidalTokenInfo?> GetValidTokensAsync()
    {
        var tokens = await LoadTokensAsync();
        
        if (tokens == null)
        {
            return null; // No saved tokens
        }
        
        if (tokens.IsValid)
        {
            return tokens; // Current tokens are still valid
        }
        
        if (tokens.IsExpired)
        {
            Console.WriteLine("⏰ Tokens are expired, attempting refresh...");
            var refreshedTokens = await RefreshTokensAsync(tokens);
            
            if (refreshedTokens != null)
            {
                await SaveTokensAsync(refreshedTokens);
                return refreshedTokens;
            }
            else
            {
                Console.WriteLine("❌ Token refresh failed, clearing expired tokens");
                await ClearTokensAsync();
                return null;
            }
        }
        
        if (tokens.NeedsRefresh)
        {
            Console.WriteLine("🔄 Tokens expiring soon, refreshing proactively...");
            var refreshedTokens = await RefreshTokensAsync(tokens);
            
            if (refreshedTokens != null)
            {
                await SaveTokensAsync(refreshedTokens);
                return refreshedTokens;
            }
            else
            {
                Console.WriteLine("⚠️ Proactive refresh failed, but current tokens still valid");
                return tokens; // Return current tokens as fallback
            }
        }
        
        return tokens;
    }
    
    public static TidalTokenInfo ParseTokenResponse(string tokenResponse)
    {
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenResponse);
        
        var accessToken = tokenData.GetProperty("access_token").GetString() ?? "";
        var refreshToken = tokenData.GetProperty("refresh_token").GetString() ?? "";
        var tokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer";
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();
        var userId = tokenData.GetProperty("user_id").GetInt64().ToString();
        
        // Extract user info
        var userInfo = tokenData.GetProperty("user");
        var countryCode = userInfo.GetProperty("countryCode").GetString() ?? "";
        var email = userInfo.GetProperty("email").GetString() ?? "";
        
        return new TidalTokenInfo
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = tokenType,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            UserId = userId,
            CountryCode = countryCode,
            Email = email
        };
    }
}