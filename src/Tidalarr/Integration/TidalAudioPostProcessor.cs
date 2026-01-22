using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Integration;

public sealed class TidalAudioPostProcessor(
    TidalDownloadClientSettings settings,
    ILogger<TidalAudioPostProcessor> logger) : IAudioPostProcessor
{
    private readonly TidalDownloadClientSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ILogger<TidalAudioPostProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private static bool? _ffmpegAvailable;

    public async Task<string> PostProcessAsync(string filePath, StreamingTrack track, StreamingQuality? quality, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.ExtractFlac)
        {
            return filePath;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return filePath;
        }

        // Only attempt extraction for M4A containers (the expected Tidal container for chunked streams).
        if (!filePath.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        if (_ffmpegAvailable != true)
        {
            _ffmpegAvailable ??= AudioFormatHandler.IsFFmpegAvailable();
            if (_ffmpegAvailable != true)
            {
                _logger.LogWarning("Extract FLAC is enabled but ffmpeg/ffprobe is not available; keeping original file: {FilePath}", filePath);
                return filePath;
            }
        }

        string codecs = AudioFormatHandler.DetectCodecs(filePath);
        if (!string.Equals(codecs, "FLAC", StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        // Avoid the built-in fallback that can produce a mislabeled .flac file if extraction fails.
        // We keep the original during extraction, and only delete it after the .flac output exists.
        string processedPath = await AudioFormatHandler.ProcessAudioFileAsync(
            filePath,
            codecs,
            extractFlac: true,
            keepOriginal: true).ConfigureAwait(false);

        if (!string.Equals(processedPath, filePath, StringComparison.OrdinalIgnoreCase) && File.Exists(processedPath))
        {
            try { File.Delete(filePath); } catch { /* best effort */ }
            return processedPath;
        }

        return filePath;
    }
}

