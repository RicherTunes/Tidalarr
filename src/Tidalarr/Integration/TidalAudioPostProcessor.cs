using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Lyrics;
using NLog;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Integration;

public sealed class TidalAudioPostProcessor(TidalDownloadClientSettings settings, ILyricsEnricher? lyricsEnricher = null) : IAudioPostProcessor
{
    private readonly TidalDownloadClientSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ILyricsEnricher? _lyricsEnricher = lyricsEnricher;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly Lazy<bool> _ffmpegAvailable = new(() => TidalAudioFormatHandler.IsFFmpegAvailable());

    public async Task<string> PostProcessAsync(string filePath, StreamingTrack track, StreamingQuality? quality, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resultPath = await ExtractFlacIfRequestedAsync(filePath, cancellationToken).ConfigureAwait(false);

        // Synced-lyrics enrichment is independent of FLAC extraction, so it runs on the final
        // audio path regardless of which codec branch above returned (gated on the opt-in settings).
        await TryEnrichLyricsAsync(resultPath, track, cancellationToken).ConfigureAwait(false);

        return resultPath;
    }

    private async Task<string> ExtractFlacIfRequestedAsync(string filePath, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// Best-effort synced-lyrics (.lrc) fetch alongside the downloaded audio, via the shared
    /// <see cref="ILyricsEnricher"/>. Canonical gating: the master
    /// <see cref="TidalDownloadClientSettings.SaveSyncedLyrics"/> toggle decides whether to enrich
    /// at all; <see cref="TidalDownloadClientSettings.UseLRCLIB"/> only gates the LRCLIB fallback
    /// (passed as <c>allowLrclibFallback</c>). Tidal supplies no native source yet, so today this
    /// effectively means "LRCLIB when both toggles are on". Never throws (other than cancellation):
    /// a lyrics failure must not fail the download.
    /// </summary>
    private async Task TryEnrichLyricsAsync(string audioFilePath, StreamingTrack track, CancellationToken cancellationToken)
    {
        if (_lyricsEnricher is null || !_settings.SaveSyncedLyrics)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
        {
            return;
        }

        try
        {
            await _lyricsEnricher.TryEnrichAsync(
                audioFilePath,
                track?.Artist?.Name ?? string.Empty,
                track?.Title ?? string.Empty,
                track?.Album?.Title ?? string.Empty,
                (int)(track?.Duration?.TotalSeconds ?? 0),
                allowLrclibFallback: _settings.UseLRCLIB,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Synced-lyrics enrichment failed for '{0}' (non-fatal)", audioFilePath);
        }
    }
}
