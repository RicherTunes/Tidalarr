using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedParsedTests
{
    private static string Base64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    private class CoreStub : ITidalCore
    {
        private readonly TidalPlaybackInfoDto _playback;
        public CoreStub(TidalPlaybackInfoDto dto) { _playback = dto; }
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo(trackId, "Song", new() { "Artist" }, "al1", "Album", 1, 100, TidalQuality.Lossless, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo(albumId, "Album", new() { "Artist" }, new(), new() { TidalQuality.Lossless }, DateTime.UtcNow, "cover", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".m4a", "application/dash+xml", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
        public Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(_playback);
    }

    private class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 }) });
    }

    private static string MpdFlac()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_flac_44100_1411"" codecs=""flac"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number%06d$.m4a"" initialization=""https://cdn.tidal.com/audio/$RepresentationID$/init.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""1"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";
    }

    private static string MpdAac()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_aac_44100_256"" codecs=""mp4a.40.5"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number%06d$.m4a"" initialization=""https://cdn.tidal.com/audio/$RepresentationID$/init.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";
    }

    [Fact(Skip = "File move semantics can lock on Windows CI; run locally to exercise end-to-end write path")]
    public async Task EnhancedDownload_ParsedMpd_Flac_NoExtraction_WhenDisabled()
    {
        var dto = new TidalPlaybackInfoDto(Base64(MpdFlac()), "application/dash+xml", "NONE", null);
        var streamSvc = new TidalStreamService(new CoreStub(dto), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath(), ExtractFlac = false };
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(dto), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);
        var outPath = Path.Combine(Path.GetTempPath(), $"tidal_enh_parsed_{Guid.NewGuid():N}");
        var res = await client.DownloadTrackEnhancedAsync("t1", outPath, TidalQuality.Lossless);

        Assert.True(res.Success, res.ErrorMessage);
        Assert.True(res.ChunkCount >= 2, $"Chunks: {res.ChunkCount}; Error: {res.ErrorMessage}");
        try { if (!string.IsNullOrEmpty(res.OutputPath) && File.Exists(res.OutputPath)) File.Delete(res.OutputPath); } catch { }
    }

    [Fact(Skip = "File move semantics can lock on Windows CI; run locally to exercise end-to-end write path")]
    public async Task EnhancedDownload_ParsedMpd_Aac_NoExtraction_EvenWhenEnabled()
    {
        var dto = new TidalPlaybackInfoDto(Base64(MpdAac()), "application/dash+xml", "NONE", null);
        var streamSvc = new TidalStreamService(new CoreStub(dto), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath(), ExtractFlac = true };
        var client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(dto), new Tidalarr.Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);
        var outPath = Path.Combine(Path.GetTempPath(), $"tidal_enh_parsed_{Guid.NewGuid():N}");
        var res = await client.DownloadTrackEnhancedAsync("t1", outPath, TidalQuality.Lossless);

        Assert.True(res.Success, res.ErrorMessage);
        Assert.True(res.ChunkCount >= 1, $"Chunks: {res.ChunkCount}; Error: {res.ErrorMessage}");
        try { if (!string.IsNullOrEmpty(res.OutputPath) && File.Exists(res.OutputPath)) File.Delete(res.OutputPath); } catch { }
    }
}




