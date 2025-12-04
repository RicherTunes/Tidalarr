using System.Net;
using System.Text;
using System.Text.Json;
using System.IO.Compression;

namespace TidalCLI;

public class TidalTokenInfo
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired;
    public bool NeedsRefresh => DateTime.UtcNow >= ExpiresAt.AddMinutes(-15);
}

public static class TokenStorage
{
    private static HttpClient CreateHttpClient()
    {
        HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        return new HttpClient(handler, disposeHandler: true);
    }

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

            string json = await File.ReadAllTextAsync(TokenFilePath);
            return JsonSerializer.Deserialize<TidalTokenInfo>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Error loading saved tokens: {ex.Message}");
            return null;
        }
    }

    public static async Task SaveTokensAsync(TidalTokenInfo tokenInfo)
    {
        try
        {
            string? directory = Path.GetDirectoryName(TokenFilePath);
            if (!Directory.Exists(directory))
                _ = Directory.CreateDirectory(directory!);

            string json = JsonSerializer.Serialize(tokenInfo, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(TokenFilePath, json);
            Console.WriteLine($"?? Tokens saved to: {TokenFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Error saving tokens: {ex.Message}");
        }
    }

    public static Task ClearTokensAsync()
    {
        try
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
                Console.WriteLine("??? Saved tokens cleared");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Error clearing tokens: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public static async Task<TidalTokenInfo?> RefreshTokensAsync(TidalTokenInfo currentTokens)
    {
        try
        {
            using HttpClient httpClient = CreateHttpClient();
            string tokenUrl = "https://auth.tidal.com/v1/oauth2/token";
            string clientId = "6BDSRdpK9hqEBTgU";
            string clientSecret = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";

            Dictionary<string, string> requestData = new()
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = currentTokens.RefreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            };

            FormUrlEncodedContent formData = new(requestData);
            HttpResponseMessage response = await httpClient.PostAsync(tokenUrl, formData);
            string responseContent = await ReadContentAsStringAsync(response.Content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("?? Tokens refreshed successfully");
                return ParseTokenResponse(responseContent);
            }

            Console.WriteLine($"? Token refresh failed: {response.StatusCode}");
            Console.WriteLine($"Response: {responseContent}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error refreshing tokens: {ex.Message}");
            return null;
        }
    }

    public static async Task<TidalTokenInfo?> GetValidTokensAsync()
    {
        TidalTokenInfo? tokens = await LoadTokensAsync();

        if (tokens == null)
        {
            return null;
        }

        if (tokens.IsValid)
        {
            return tokens;
        }

        if (tokens.IsExpired)
        {
            Console.WriteLine("? Tokens are expired, attempting refresh...");
            TidalTokenInfo? refreshedTokens = await RefreshTokensAsync(tokens);

            if (refreshedTokens != null)
            {
                await SaveTokensAsync(refreshedTokens);
                return refreshedTokens;
            }

            Console.WriteLine("? Token refresh failed, clearing expired tokens");
            await ClearTokensAsync();
            return null;
        }

        if (tokens.NeedsRefresh)
        {
            Console.WriteLine("?? Tokens expiring soon, refreshing proactively...");
            TidalTokenInfo? refreshedTokens = await RefreshTokensAsync(tokens);

            if (refreshedTokens != null)
            {
                await SaveTokensAsync(refreshedTokens);
                return refreshedTokens;
            }

            Console.WriteLine("?? Proactive refresh failed, but current tokens still valid");
            return tokens;
        }

        return tokens;
    }

    public static TidalTokenInfo ParseTokenResponse(string tokenResponse)
    {
        JsonElement tokenData = JsonSerializer.Deserialize<JsonElement>(tokenResponse);

        string accessToken = tokenData.GetProperty("access_token").GetString() ?? string.Empty;
        string refreshToken = tokenData.GetProperty("refresh_token").GetString() ?? string.Empty;
        string tokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer";
        int expiresIn = tokenData.GetProperty("expires_in").GetInt32();
        string userId = tokenData.TryGetProperty("user_id", out JsonElement userIdProp) ? userIdProp.GetInt64().ToString() : string.Empty;

        string sessionId = string.Empty;
        string countryCode = string.Empty;
        string email = string.Empty;

        if (tokenData.TryGetProperty("user", out JsonElement userInfo) && userInfo.ValueKind == JsonValueKind.Object)
        {
            if (userInfo.TryGetProperty("sessionId", out JsonElement sessionProp))
                sessionId = sessionProp.GetString() ?? string.Empty;
            if (userInfo.TryGetProperty("countryCode", out JsonElement ccProp))
                countryCode = ccProp.GetString() ?? string.Empty;
            if (userInfo.TryGetProperty("email", out JsonElement emailProp))
                email = emailProp.GetString() ?? string.Empty;
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            try
            {
                string[] parts = accessToken.Split('.');
                if (parts.Length == 3)
                {
                    string payload = parts[1];
                    payload += new string('=', (4 - (payload.Length % 4)) % 4);
                    byte[] payloadBytes = Convert.FromBase64String(payload);
                    using JsonDocument payloadDoc = JsonDocument.Parse(payloadBytes);
                    if (payloadDoc.RootElement.TryGetProperty("sid", out JsonElement sidProp))
                        sessionId = sidProp.GetString() ?? string.Empty;
                }
            }
            catch
            {
                // ignore decode issues
            }
        }

        return new TidalTokenInfo
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = tokenType,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            UserId = userId,
            SessionId = sessionId,
            CountryCode = countryCode,
            Email = email
        };
    }


    private static async Task<string> ReadContentAsStringAsync(HttpContent content)
    {
        byte[] bytes = await content.ReadAsByteArrayAsync().ConfigureAwait(false);

        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            using MemoryStream compressed = new(bytes);
            using GZipStream gzip = new(compressed, CompressionMode.Decompress);
            using StreamReader reader = new(gzip, Encoding.UTF8);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        Encoding encoding = Encoding.UTF8;
        string? charset = content.Headers?.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset);
            }
            catch
            {
                encoding = Encoding.UTF8;
            }
        }

        return encoding.GetString(bytes);
    }


}


