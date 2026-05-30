using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Application.Services;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// The real Lidarr download path runs through SimpleDownloadOrchestrator -> TidalAudioPostProcessor.
/// Synced-lyrics enrichment (LRCLIB) must run there, gated on the opt-in settings
/// (SaveSyncedLyrics AND UseLRCLIB) — it previously only existed on a dead code path.
/// </summary>
public class TidalAudioPostProcessorLyricsTests
{
    private sealed class RecordingLyricsEnricher : ILyricsEnricher
    {
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastArtist { get; private set; }
        public string? LastTrack { get; private set; }
        public string? LastAlbum { get; private set; }
        public int LastDuration { get; private set; }

        public Task TryEnrichAsync(string audioFilePath, string artistName, string trackName, string albumName, int durationSeconds, CancellationToken ct = default)
        {
            Calls++;
            LastPath = audioFilePath;
            LastArtist = artistName;
            LastTrack = trackName;
            LastAlbum = albumName;
            LastDuration = durationSeconds;
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
    public async Task Fetches_lyrics_when_SaveSyncedLyrics_and_UseLRCLIB_enabled()
    {
        var settings = new TidalDownloadClientSettings { ExtractFlac = false, SaveSyncedLyrics = true, UseLRCLIB = true };
        var enricher = new RecordingLyricsEnricher();
        var sut = new TidalAudioPostProcessor(settings, enricher);
        var file = CreateTempAudio();

        try
        {
            await sut.PostProcessAsync(file, SampleTrack(), null, CancellationToken.None);

            Assert.Equal(1, enricher.Calls);
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

    [Theory]
    [InlineData(false, true)]   // master toggle off
    [InlineData(true, false)]   // LRCLIB opt-in off (privacy default)
    [InlineData(false, false)]
    public async Task Does_not_fetch_lyrics_when_either_flag_disabled(bool saveSyncedLyrics, bool useLrclib)
    {
        var settings = new TidalDownloadClientSettings { ExtractFlac = false, SaveSyncedLyrics = saveSyncedLyrics, UseLRCLIB = useLrclib };
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
