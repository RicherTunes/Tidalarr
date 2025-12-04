namespace Tidalarr.Domain.Streaming;

public static class AudioFormatHandler
{
    public static async Task<string> ProcessAudioFileAsync(
        string inputPath,
        string codecs,
        bool extractFlac = true,
        bool keepOriginal = false,
        IAudioProcessor? audio = null)
    {
        try
        {
            audio ??= new SystemAudioProcessor();
            if (codecs == "FLAC" && extractFlac)
            {
                // Extract FLAC from M4A container
                Console.WriteLine("🎵 Extracting FLAC from M4A container...");
                string flacPath = Path.ChangeExtension(inputPath, "flac");

                bool success = await ExtractFlacFromM4AAsync(inputPath, flacPath, audio, keepOriginal);
                if (success)
                {
                    if (!keepOriginal && File.Exists(inputPath))
                    {
                        File.Delete(inputPath);
                    }
                    return flacPath;
                }
                else
                {
                    Console.WriteLine("⚠️ FLAC extraction failed, keeping M4A file");
                    return inputPath;
                }
            }

            return inputPath; // Keep original M4A file
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error processing audio file: {ex.Message}");
            return inputPath; // Return original path on error
        }
    }

    private static async Task<bool> ExtractFlacFromM4AAsync(string inputPath, string outputPath, IAudioProcessor audio, bool keepOriginal)
    {
        try
        {
            string ffmpegArgs = $"-i \"{inputPath}\" -c copy \"{outputPath}\"";
            (int exitCode, string _, string stderr) = await audio.RunFfmpegAsync(ffmpegArgs);
            bool success = exitCode == 0 && File.Exists(outputPath);
            if (!success)
            {
                Console.WriteLine($"⚠️ FFmpeg error: {stderr}");
                return !keepOriginal && TryCopyFallback(inputPath, outputPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ FFmpeg extraction failed: {ex.Message}");
            return !keepOriginal && TryCopyFallback(inputPath, outputPath);
        }
    }

    private static bool TryCopyFallback(string inputPath, string outputPath)
    {
        try
        {
            File.Copy(inputPath, outputPath, true);
            Console.WriteLine("📝 Note: File copied as-is (FLAC still in M4A container)");
            return true;
        }
        catch
        {
            return false;
        }
    }


    public static string DetectCodecs(string filePath)
    {
        try
        {
            // Use ffprobe to detect codecs
            SystemAudioProcessor ap = new SystemAudioProcessor();
            (int exitCode, string stdout, string _) = ap.RunFfprobe($"-v quiet -select_streams a:0 -show_entries stream=codec_name -of csv=p=0 \"{filePath}\"");
            if (exitCode == 0)
            {
                string codec = stdout.Trim();
                return codec.ToLowerInvariant() switch
                {
                    "flac" => "FLAC",
                    "aac" => "MP4A",
                    _ => "MP4A"
                };
            }
        }
        catch
        {
            // Ignore errors, return default
        }

        return "MP4A"; // Default assumption
    }

    public static bool IsFFmpegAvailable()
    {
        try
        {
            SystemAudioProcessor ap = new SystemAudioProcessor();
            (int exitCode, string _, string _) = ap.RunFfprobe("-version");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

public class AudioFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Codecs { get; set; } = string.Empty;
    public bool IsFlacInM4A { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TidalDownloadResult
{
    public bool Success { get; set; }
    public string TrackId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public AudioFileInfo FileInfo { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string Quality { get; set; } = string.Empty;
}

