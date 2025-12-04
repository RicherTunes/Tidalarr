using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalDownloadClientEnhancedParsedTests
{
    private static string Base64(string s)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

    private class CoreStub(TidalPlaybackInfoDto dto) : ITidalCore
    {
        private readonly TidalPlaybackInfoDto _playback = dto;

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "Song", new() { "Artist" }, "al1", "Album", 1, 100, TidalQuality.Lossless, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo(albumId, "Album", new() { "Artist" }, new(), new() { TidalQuality.Lossless }, DateTime.UtcNow, "cover", true));
        }

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TidalTrackInfo>());
        }

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return GetAlbumAsync(albumId, cancellationToken);
        }

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalSearchResults(new(), new(), 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, [], ".m4a", "application/dash+xml", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }

        public Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._playback);
        }
    }

    private class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4, 5]) });
        }
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

    [Fact]
    public async Task EnhancedDownload_ParsedMpd_Flac_NoExtraction_WhenDisabled()
    {
        TidalPlaybackInfoDto dto = new TidalPlaybackInfoDto(Base64(MpdFlac()), "application/dash+xml", "NONE", null);
        TidalStreamService streamSvc = new TidalStreamService(new CoreStub(dto), new TidalManifestParser());
        TidalChunkDownloader downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        TidalDownloadClientSettings settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath(), ExtractFlac = false };
        TidalDownloadClient client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(dto), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);
        string outPath = Path.Combine(Path.GetTempPath(), $"tidal_enh_parsed_{Guid.NewGuid():N}");
        EnhancedDownloadResult res = await client.DownloadTrackEnhancedAsync("t1", outPath, TidalQuality.Lossless);

        Assert.True(res.Success, res.ErrorMessage);
        Assert.True(res.ChunkCount >= 2, $"Chunks: {res.ChunkCount}; Error: {res.ErrorMessage}");
        try { if (!string.IsNullOrEmpty(res.OutputPath) && File.Exists(res.OutputPath)) File.Delete(res.OutputPath); } catch { }
    }

    [Fact]
    public async Task EnhancedDownload_ParsedMpd_Aac_NoExtraction_EvenWhenEnabled()
    {
        TidalPlaybackInfoDto dto = new TidalPlaybackInfoDto(Base64(MpdAac()), "application/dash+xml", "NONE", null);
        TidalStreamService streamSvc = new TidalStreamService(new CoreStub(dto), new TidalManifestParser());
        TidalChunkDownloader downloader = new TidalChunkDownloader(new HttpClient(new OkHandler()));
        TidalDownloadClientSettings settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath(), ExtractFlac = true };
        TidalDownloadClient client = new TidalDownloadClient(streamSvc, downloader, new CoreStub(dto), new Domain.Quality.TidalQualityDetector(), settings, NullLogger.Instance);
        string outPath = Path.Combine(Path.GetTempPath(), $"tidal_enh_parsed_{Guid.NewGuid():N}");
        EnhancedDownloadResult res = await client.DownloadTrackEnhancedAsync("t1", outPath, TidalQuality.Lossless);

        Assert.True(res.Success, res.ErrorMessage);
        Assert.True(res.ChunkCount >= 1, $"Chunks: {res.ChunkCount}; Error: {res.ErrorMessage}");
        try { if (!string.IsNullOrEmpty(res.OutputPath) && File.Exists(res.OutputPath)) File.Delete(res.OutputPath); } catch { }
    }
}




