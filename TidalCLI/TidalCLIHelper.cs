using System.Net;
using System.Text.Json;
using Lidarr.Plugin.Common.Security;
using TidalQuality = Tidalarr.Core.Models.TidalQuality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Tidalarr.Core.Models;

namespace TidalCLI;

/// <summary>
/// Helper class that demonstrates proper plugin usage from CLI
/// </summary>
public static class TidalCLIHelper
{
    private static HttpClient CreateHttpClient()
    {
        HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        return new HttpClient(handler, disposeHandler: true);
    }

    public static async Task<string> TestRealDownloadWorkflowAsync(string trackId, TidalTokenInfo tokens)
    {
        using HttpClient httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"{tokens.TokenType} {tokens.AccessToken}");

        string sessionId = string.IsNullOrEmpty(tokens.SessionId) ? tokens.UserId : tokens.SessionId;

        try
        {
            Console.WriteLine("\n?? Step 1: Getting track information...");
            string trackInfoUrl = $"https://api.tidal.com/v1/tracks/{trackId}?sessionId={sessionId}&countryCode={tokens.CountryCode}";

            HttpResponseMessage trackInfoResponse = await httpClient.GetAsync(trackInfoUrl);
            string trackInfoContent = await trackInfoResponse.Content.ReadAsStringAsync();

            if (!trackInfoResponse.IsSuccessStatusCode)
            {
                return $"? Failed to get track info: {trackInfoResponse.StatusCode}\nResponse: {trackInfoContent}";
            }

            JsonElement trackInfo = JsonSerializer.Deserialize<JsonElement>(trackInfoContent);
            string? title = trackInfo.GetProperty("title").GetString();
            string? artist = trackInfo.GetProperty("artist").GetProperty("name").GetString();
            int duration = trackInfo.GetProperty("duration").GetInt32();
            string? quality = trackInfo.TryGetProperty("audioQuality", out JsonElement q) ? q.GetString() : "LOSSLESS";

            Console.WriteLine($"? Track: {title} by {artist}");
            Console.WriteLine($"   Duration: {TimeSpan.FromSeconds(duration):mm\\:ss}");
            Console.WriteLine($"   Quality: {quality}");

            Console.WriteLine("\n?? Step 2: Getting stream manifest...");
            string streamUrl = $"https://api.tidal.com/v1/tracks/{trackId}/playbackinfopostpaywall?audioquality={quality}&playbackmode=STREAM&assetpresentation=FULL&sessionId={sessionId}&countryCode={tokens.CountryCode}";

            HttpResponseMessage streamResponse = await httpClient.GetAsync(streamUrl);
            string streamContent = await streamResponse.Content.ReadAsStringAsync();

            if (!streamResponse.IsSuccessStatusCode)
            {
                return $"? Failed to get stream manifest: {streamResponse.StatusCode}\nResponse: {streamContent}";
            }

            JsonElement streamInfo = JsonSerializer.Deserialize<JsonElement>(streamContent);
            string encryptionType = streamInfo.TryGetProperty("encryptionType", out JsonElement encProp) ? encProp.GetString() ?? "NONE" : "NONE";
            string? securityToken = streamInfo.TryGetProperty("securityToken", out JsonElement tokenProp) ? tokenProp.GetString() : null;
            bool isEncrypted = !string.Equals(encryptionType, "NONE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(securityToken);

            Console.WriteLine("\n?? Step 3: Parsing DASH manifest using plugin...");
            StreamManifest manifest = new(streamInfo);
            Console.WriteLine("? Manifest parsed successfully!");
            Console.WriteLine($"   Format: {manifest.FileExtension} container");
            Console.WriteLine($"   Codec: {manifest.Codecs}");
            Console.WriteLine($"   Chunks: {manifest.ChunkUrls.Length}");
            Console.WriteLine($"   Encrypted: {isEncrypted}");
            if (manifest.ChunkUrls.Length == 0)
            {
                return "? No chunks found in manifest";
            }
            Console.WriteLine("\n?? Step 4: Downloading using plugin's chunk downloader...");
            TidalChunkDownloader chunkDownloader = new(httpClient);
            string mime = manifest.MimeType == ManifestMimeType.BTS ? "application/vnd.tidal.bts" : "application/dash+xml";
            TidalStreamInfo streamInfoModel = new(
                trackId,
                manifest.ChunkUrls,
                manifest.FileExtension,
                mime,
                isEncrypted,
                securityToken);

            using Stream audioStream = await chunkDownloader.DownloadAndAssembleAsync(streamInfoModel, progress: null);

            Console.WriteLine("\n?? Step 5: Saving assembled audio file...");
            string fileName = Sanitize.PathSegment($"{artist} - {title}");
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "Unknown";
            }

            string outputPath = Path.Combine(Path.GetTempPath(), $"tidalarr_{fileName}{manifest.FileExtension}");

            await using (FileStream fileStream = new(outputPath, FileMode.Create, FileAccess.Write))
            {
                audioStream.Position = 0;
                await audioStream.CopyToAsync(fileStream);
            }

            long fileSize = new FileInfo(outputPath).Length;
            Console.WriteLine($"? Audio file saved: {outputPath}");
            Console.WriteLine($"   Size: {fileSize / 1024 / 1024:F2} MB");

            if (manifest.Codecs == "FLAC")
            {
                Console.WriteLine("\n?? Step 6: Processing FLAC extraction using plugin...");
                if (AudioFormatHandler.IsFFmpegAvailable())
                {
                    string processedPath = await AudioFormatHandler.ProcessAudioFileAsync(
                        outputPath, manifest.Codecs, extractFlac: true, keepOriginal: false);
                    if (!string.Equals(processedPath, outputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"? FLAC extracted: {processedPath}");
                        outputPath = processedPath;
                    }
                }
                else
                {
                    Console.WriteLine("?? FFmpeg not available - keeping M4A file with FLAC inside");
                }
            }

            return $"?? COMPLETE PLUGIN-BASED DOWNLOAD SUCCESS!\n" +
                   $"? Downloaded: {outputPath}\n" +
                   $"? Used plugin's DASH manifest parser\n" +
                   $"? Used plugin's chunk downloader\n" +
                   $"? Used plugin's audio format handler\n" +
                   $"? Proper M4A container with {manifest.Codecs} handling";
        }
        catch (Exception ex)
        {
            return $"? Error during plugin-based download test: {ex.Message}";
        }
    }

    public static TidalarrSettings CreateTestIndexerSettings()
    {
        return new TidalarrSettings
        {
            TidalMarket = "US",
            RedirectUrl = "https://tidal.com/android/login/auth?code=test_code&state=test_state",
            ConfigPath = Path.Combine(Path.GetTempPath(), "tidalarr-test"),
            EnableCache = true,
            CacheDuration = 15
        };
    }

    public static TidalarrSettings CreateTestDownloadSettings()
    {
        return new TidalarrSettings
        {
            PreferredQuality = TidalQuality.Lossless,
            IncludeMqa = true,
            DownloadPath = Path.Combine(Path.GetTempPath(), "tidalarr-downloads")
        };
    }
}







