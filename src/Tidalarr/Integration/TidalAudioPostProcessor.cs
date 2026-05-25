using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Interfaces;
using NLog;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Integration;

public sealed class TidalAudioPostProcessor(TidalDownloadClientSettings settings) : IAudioPostProcessor
{
    private readonly TidalDownloadClientSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly Lazy<bool> _ffmpegAvailable = new(() => TidalAudioFormatHandler.IsFFmpegAvailable());

    public async Task<string> PostProcessAsync(string filePath, StreamingTrack track, StreamingQuality? quality, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!this._settings.ExtractFlac)
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

        if (!_ffmpegAvailable.Value)
        {
            Logger.Warn("Extract FLAC is enabled but ffmpeg/ffprobe is not available; keeping original file: {0}", filePath);
            return filePath;
        }

        string codecs = TidalAudioFormatHandler.DetectCodecs(filePath);
        if (!string.Equals(codecs, "FLAC", StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        // Avoid the built-in fallback that can produce a mislabeled .flac file if extraction fails.
        // We keep the original during extraction, and only delete it after the .flac output exists.
        string processedPath = await TidalAudioFormatHandler.ProcessAudioFileAsync(
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
