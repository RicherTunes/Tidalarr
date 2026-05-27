using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Services.Lyrics;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Application.Services;

public interface ILyricsEnricher : IDisposable
{
    Task TryEnrichAsync(string audioFilePath, string artistName, string trackName, string albumName, int durationSeconds, CancellationToken ct = default);
}

public sealed class LyricsEnricher : ILyricsEnricher
{
    private readonly LrclibClient _client = new();
    private readonly ILogger? _logger;

    public LyricsEnricher(ILogger? logger = null) => _logger = logger;

    public async Task TryEnrichAsync(string audioFilePath, string artistName, string trackName, string albumName, int durationSeconds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(trackName))
            return;

        try
        {
            var lyrics = await _client.TryFetchSyncedLyricsAsync(artistName, trackName, albumName, durationSeconds, ct).ConfigureAwait(false);
            if (lyrics is null) return;

            var lrcPath = Path.ChangeExtension(audioFilePath, ".lrc");
            await File.WriteAllTextAsync(lrcPath, lyrics, ct).ConfigureAwait(false);
            _logger?.LogDebug("Saved synced lyrics: {File}", Path.GetFileName(lrcPath));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Lyrics fetch failed for {Artist} — {Track} (non-fatal)", artistName, trackName);
        }
    }

    public void Dispose() => _client.Dispose();
}
