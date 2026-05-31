using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Services.Lyrics;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// The real Lidarr download path runs through SimpleDownloadOrchestrator -> TidalAudioPostProcessor,
/// which delegates lyrics to Common's shared <see cref="ILyricsEnricher"/>. Canonical gating:
/// <c>SaveSyncedLyrics</c> decides whether to enrich at all; <c>UseLRCLIB</c> is passed through as
/// the LRCLIB-fallback gate (a native source, when one exists, is always tried by the enricher).
/// </summary>
public class TidalAudioPostProcessorLyricsTests
{
    private sealed class RecordingLyricsEnricher : ILyricsEnricher
    {
        public int Calls { get; private set; }
        public bool LastAllowLrclibFallback { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastArtist { get; private set; }
        public string? LastTrack { get; private set; }
        public string? LastAlbum { get; private set; }
        public int LastDuration { get; private set; }

        public Task TryEnrichAsync(string audioFilePath, string artistName, string trackName, string albumName, int durationSeconds, bool allowLrclibFallback, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastPath = audioFilePath;
            LastArtist = artistName;
            LastTrack = trackName;
            LastAlbum = albumName;
            LastDuration = durationSeconds;
            LastAllowLrclibFallback = allowLrclibFallback;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private static StreamingTrack SampleTrack() => new()
    {
        Title = "Song",
        Artist = new StreamingArtist { Name = "Artist" },
        Album = new StreamingAlbum { Title = "Album" },
        Duration = TimeSpan.FromSeconds(200),
    };

    private static string CreateTempAudio()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tidal-lyrics-test-{Guid.NewGuid():N}.flac");
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public async Task Invokes_enricher_with_fallback_on_when_both_toggles_enabled()
    {
        var settings = new TidalDownloadClientSettings { ExtractFlac = false, SaveSyncedLyrics = true, UseLRCLIB = true };
        var enricher = new RecordingLyricsEnricher();
        var sut = new TidalAudioPostProcessor(settings, enricher);
        var file = CreateTempAudio();

        try
        {
            await sut.PostProcessAsync(file, SampleTrack(), null, CancellationToken.None);

            Assert.Equal(1, enricher.Calls);
            Assert.True(enricher.LastAllowLrclibFallback);
            Assert.Equal(file, enricher.LastPath);
            Assert.Equal("Artist", enricher.LastArtist);
            Assert.Equal("Song", enricher.LastTrack);
            Assert.Equal("Album", enricher.LastAlbum);
            Assert.Equal(200, enricher.LastDuration);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Invokes_enricher_with_fallback_off_when_UseLRCLIB_disabled()
    {
        var settings = new TidalDownloadClientSettings { ExtractFlac = false, SaveSyncedLyrics = true, UseLRCLIB = false };
        var enricher = new RecordingLyricsEnricher();
        var sut = new TidalAudioPostProcessor(settings, enricher);
        var file = CreateTempAudio();

        try
        {
            await sut.PostProcessAsync(file, SampleTrack(), null, CancellationToken.None);

            // Canonical: still invoked under SaveSyncedLyrics (a native source would be tried),
            // but the LRCLIB fallback is gated off.
            Assert.Equal(1, enricher.Calls);
            Assert.False(enricher.LastAllowLrclibFallback);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Does_not_invoke_enricher_when_SaveSyncedLyrics_disabled()
    {
        var settings = new TidalDownloadClientSettings { ExtractFlac = false, SaveSyncedLyrics = false, UseLRCLIB = true };
        var enricher = new RecordingLyricsEnricher();
        var sut = new TidalAudioPostProcessor(settings, enricher);
        var file = CreateTempAudio();

        try
        {
            await sut.PostProcessAsync(file, SampleTrack(), null, CancellationToken.None);

            Assert.Equal(0, enricher.Calls);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
