using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Tidalarr.Domain.Quality;

namespace TidalCLI;

/// <summary>
/// Helper class that demonstrates proper plugin usage from CLI
/// </summary>
public static class TidalCLIHelper
{
    public static async Task<string> TestRealDownloadWorkflowAsync(string trackId, TidalTokenInfo tokens)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"{tokens.TokenType} {tokens.AccessToken}");
        
        try
        {
            // Step 1: Get track info
            Console.WriteLine("\n📋 Step 1: Getting track information...");
            var trackInfoUrl = $"https://api.tidal.com/v1/tracks/{trackId}?sessionId={tokens.UserId}&countryCode={tokens.CountryCode}";
            
            var trackInfoResponse = await httpClient.GetAsync(trackInfoUrl);
            var trackInfoContent = await trackInfoResponse.Content.ReadAsStringAsync();
            
            if (!trackInfoResponse.IsSuccessStatusCode)
            {
                return $"❌ Failed to get track info: {trackInfoResponse.StatusCode}\nResponse: {trackInfoContent}";
            }
            
            var trackInfo = JsonSerializer.Deserialize<JsonElement>(trackInfoContent);
            var title = trackInfo.GetProperty("title").GetString();
            var artist = trackInfo.GetProperty("artist").GetProperty("name").GetString();
            var duration = trackInfo.GetProperty("duration").GetInt32();
            var quality = trackInfo.TryGetProperty("audioQuality", out var q) ? q.GetString() : "LOSSLESS";
            
            Console.WriteLine($"✅ Track: {title} by {artist}");
            Console.WriteLine($"   Duration: {TimeSpan.FromSeconds(duration):mm\\:ss}");
            Console.WriteLine($"   Quality: {quality}");
            
            // Step 2: Get stream manifest
            Console.WriteLine("\n📡 Step 2: Getting stream manifest...");
            var streamUrl = $"https://api.tidal.com/v1/tracks/{trackId}/playbackinfopostpaywall?audioquality={quality}&playbackmode=STREAM&assetpresentation=FULL&sessionId={tokens.UserId}&countryCode={tokens.CountryCode}";
            
            var streamResponse = await httpClient.GetAsync(streamUrl);
            var streamContent = await streamResponse.Content.ReadAsStringAsync();
            
            if (!streamResponse.IsSuccessStatusCode)
            {
                return $"❌ Failed to get stream manifest: {streamResponse.StatusCode}\nResponse: {streamContent}";
            }
            
            var streamInfo = JsonSerializer.Deserialize<JsonElement>(streamContent);
            
            // Step 3: Use plugin's DASH manifest parser
            Console.WriteLine("\n🔍 Step 3: Parsing DASH manifest using plugin...");
            var manifest = new StreamManifest(streamInfo);
            
            Console.WriteLine($"✅ Manifest parsed successfully!");
            Console.WriteLine($"   Format: {manifest.FileExtension} container");
            Console.WriteLine($"   Codec: {manifest.Codecs}");
            Console.WriteLine($"   Chunks: {manifest.ChunkUrls.Length}");
            Console.WriteLine($"   Encrypted: {!string.IsNullOrEmpty(manifest.EncryptionKey)}");
            
            if (manifest.ChunkUrls.Length > 0)
            {
                // Step 4: Use plugin's chunk downloader
                Console.WriteLine("\n⬇️ Step 4: Downloading using plugin's chunk downloader...");
                
                var chunkDownloader = new TidalChunkDownloader(httpClient);
                // Build legacy stream info model compatible with downloader overload
                var mime = manifest.MimeType == ManifestMimeType.BTS ? "application/vnd.tidal.bts" : "application/dash+xml";
                var streamInfoModel = new Tidalarr.Core.Models.TidalStreamInfo(
                    trackId,
                    manifest.ChunkUrls,
                    manifest.FileExtension,
                    mime,
                    !string.IsNullOrEmpty(manifest.EncryptionKey),
                    manifest.EncryptionKey);
                
                using var audioStream = await chunkDownloader.DownloadAndAssembleAsync(streamInfoModel, progress: null);
                
                // Step 5: Save to file
                Console.WriteLine("\n💾 Step 5: Saving assembled audio file...");
                var fileName = $"{artist} - {title}";
                foreach (var c in Path.GetInvalidFileNameChars().Concat(new[] { ':', '?', '*', '<', '>', '|' }))
                {
                    fileName = fileName.Replace(c, '_');
                }
                var outputPath = Path.Combine(Path.GetTempPath(), $"tidalarr_{fileName}{manifest.FileExtension}");
                
                await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                audioStream.Position = 0;
                await audioStream.CopyToAsync(fileStream);
                
                var fileSize = new FileInfo(outputPath).Length;
                Console.WriteLine($"✅ Audio file saved: {outputPath}");
                Console.WriteLine($"   Size: {fileSize / 1024 / 1024:F2} MB");
                
                // Step 6: Use plugin's audio format handler
                if (manifest.Codecs == "FLAC")
                {
                    Console.WriteLine("\n🎵 Step 6: Processing FLAC extraction using plugin...");
                    
                    if (AudioFormatHandler.IsFFmpegAvailable())
                    {
                        var processedPath = await AudioFormatHandler.ProcessAudioFileAsync(
                            outputPath, manifest.Codecs, extractFlac: true, keepOriginal: false);
                            
                        if (processedPath != outputPath)
                        {
                            Console.WriteLine($"✅ FLAC extracted: {processedPath}");
                            outputPath = processedPath;
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠️ FFmpeg not available - keeping M4A file with FLAC inside");
                    }
                }
                
                return $"🎉 COMPLETE PLUGIN-BASED DOWNLOAD SUCCESS!\n" +
                       $"✅ Downloaded: {outputPath}\n" +
                       $"✅ Used plugin's DASH manifest parser\n" +
                       $"✅ Used plugin's chunk downloader\n" +
                       $"✅ Used plugin's audio format handler\n" +
                       $"✅ Proper M4A container with {manifest.Codecs} handling";
            }
            else
            {
                return "❌ No chunks found in manifest";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Error during plugin-based download test: {ex.Message}";
        }
        finally
        {
            httpClient?.Dispose();
        }
    }
    
    public static TidalIndexerSettings CreateTestIndexerSettings()
    {
        return new TidalIndexerSettings
        {
            TidalMarket = "US",
            RedirectUrl = "https://tidal.com/android/login/auth?code=test_code&state=test_state",
            ConfigPath = Path.Combine(Path.GetTempPath(), "tidalarr-test"),
            EnableCache = true,
            CacheDuration = 15
        };
    }
    
    public static TidalDownloadSettings CreateTestDownloadSettings()
    {
        return new TidalDownloadSettings
        {
            PreferredQuality = "Lossless",
            IncludeMqa = true,
            DownloadPath = Path.Combine(Path.GetTempPath(), "tidalarr-downloads")
        };
    }
}
