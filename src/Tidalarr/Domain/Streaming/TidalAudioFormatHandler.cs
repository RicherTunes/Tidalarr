namespace Tidalarr.Domain.Streaming;

public static class AudioFormatHandler
{
    public static bool IsFFmpegAvailable(IAudioProcessor? audio = null)
    {
        IAudioProcessor processor = audio ?? new SystemAudioProcessor();

        try
        {
            (int exitCode, _, _) = processor.RunFfprobe("-version");
            if (exitCode != 0) return false;

            // `RunFfmpegAsync` is async; keep the availability check sync-friendly by relying on ffprobe.
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string DetectCodecs(string filePath, IAudioProcessor? audio = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return string.Empty;
        }

        IAudioProcessor processor = audio ?? new SystemAudioProcessor();

        string args = $"-v error -select_streams a:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 {QuoteArg(filePath)}";

        try
        {
            (int exitCode, string stdout, _) = processor.RunFfprobe(args);
            if (exitCode != 0) return string.Empty;

            string codec = (stdout ?? string.Empty).Trim();
            if (codec.Length == 0) return string.Empty;

            return codec.ToUpperInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static async Task<string> ProcessAudioFileAsync(
        string inputPath,
        string codecs,
        bool extractFlac,
        bool keepOriginal,
        IAudioProcessor? audio = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!extractFlac)
        {
            return inputPath;
        }

        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return inputPath;
        }

        if (!string.Equals(codecs, "FLAC", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        // Tidal delivers FLAC audio in an M4A container; extraction only makes sense for M4A sources.
        if (!inputPath.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        IAudioProcessor processor = audio ?? new SystemAudioProcessor();
        string outputPath = Path.ChangeExtension(inputPath, ".flac");

        // Ensure we don't keep stale output around from a prior failed attempt.
        if (File.Exists(outputPath))
        {
            try { File.Delete(outputPath); } catch { /* best effort */ }
        }

        string args = string.Join(" ", new[]
        {
            "-y",
            "-hide_banner",
            "-loglevel error",
            "-i", QuoteArg(inputPath),
            "-map 0:a:0",
            "-c:a copy",
            QuoteArg(outputPath)
        });

        try
        {
            (int exitCode, _, _) = await processor.RunFfmpegAsync(args, cancellationToken).ConfigureAwait(false);

            if (exitCode == 0 && File.Exists(outputPath))
            {
                if (!keepOriginal)
                {
                    try { File.Delete(inputPath); } catch { /* best effort */ }
                }

                return outputPath;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Fall through to cleanup + return input path.
        }

        // Never produce a mislabeled .flac file as a fallback; on failure keep the original file.
        if (File.Exists(outputPath))
        {
            try { File.Delete(outputPath); } catch { /* best effort */ }
        }

        return inputPath;
    }

    private static string QuoteArg(string value)
    {
        // Keep quoting predictable for ffmpeg/ffprobe across platforms.
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
