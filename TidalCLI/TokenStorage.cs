using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Threading.Tasks;

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
        var handler = new HttpClientHandler
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

            var json = await File.ReadAllTextAsync(TokenFilePath);
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
            var directory = Path.GetDirectoryName(TokenFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory!);

            var json = JsonSerializer.Serialize(tokenInfo, new JsonSerializerOptions
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
            using var httpClient = CreateHttpClient();
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
            var responseContent = await ReadContentAsStringAsync(response.Content);

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
        var tokens = await LoadTokensAsync();

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
            var refreshedTokens = await RefreshTokensAsync(tokens);

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
            var refreshedTokens = await RefreshTokensAsync(tokens);

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
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenResponse);

        var accessToken = tokenData.GetProperty("access_token").GetString() ?? string.Empty;
        var refreshToken = tokenData.GetProperty("refresh_token").GetString() ?? string.Empty;
        var tokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer";
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();
        var userId = tokenData.TryGetProperty("user_id", out var userIdProp) ? userIdProp.GetInt64().ToString() : string.Empty;

        var sessionId = string.Empty;
        var countryCode = string.Empty;
        var email = string.Empty;

        if (tokenData.TryGetProperty("user", out var userInfo) && userInfo.ValueKind == JsonValueKind.Object)
        {
            if (userInfo.TryGetProperty("sessionId", out var sessionProp))
                sessionId = sessionProp.GetString() ?? string.Empty;
            if (userInfo.TryGetProperty("countryCode", out var ccProp))
                countryCode = ccProp.GetString() ?? string.Empty;
            if (userInfo.TryGetProperty("email", out var emailProp))
                email = emailProp.GetString() ?? string.Empty;
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            try
            {
                var parts = accessToken.Split('.');
                if (parts.Length == 3)
                {
                    var payload = parts[1];
                    payload += new string('=', (4 - payload.Length % 4) % 4);
                    var payloadBytes = Convert.FromBase64String(payload);
                    using (var payloadDoc = JsonDocument.Parse(payloadBytes))
                    {
                        if (payloadDoc.RootElement.TryGetProperty("sid", out var sidProp))
                            sessionId = sidProp.GetString() ?? string.Empty;
                    }
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
        var bytes = await content.ReadAsByteArrayAsync().ConfigureAwait(false);

        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            using var compressed = new MemoryStream(bytes);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        Encoding encoding = Encoding.UTF8;
        var charset = content.Headers?.ContentType?.CharSet;
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

