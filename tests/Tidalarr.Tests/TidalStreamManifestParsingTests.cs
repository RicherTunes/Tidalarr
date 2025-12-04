using System.Text;
using System.Text.Json;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalStreamManifestParsingTests
{
    private static string Base64(string s)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

    [Fact]
    public void StreamManifest_MPD_ParsesChunkUrlsAndCodec()
    {
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_flac_44100_1411"" codecs=""flac"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number%06d$.m4a"" initialization=""https://cdn.tidal.com/audio/$RepresentationID$/init.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""2"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml),
            keyId = "kid-123"
        });

        StreamManifest sm = new StreamManifest(json);
        Assert.Equal(ManifestMimeType.MPD, sm.MimeType);
        Assert.Equal("FLAC", sm.Codecs);
        Assert.Equal(".m4a", sm.FileExtension);
        Assert.Equal("kid-123", sm.KeyId);
        Assert.True(sm.ChunkUrls.Length >= 3); // init + segments
        Assert.Contains("init.m4a", sm.ChunkUrls[0]);
        Assert.Contains("seg_", sm.ChunkUrls[^1]);
    }

    [Fact]
    public void StreamManifest_BTS_UsesDirectUrl()
    {
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a"
        });

        StreamManifest sm = new StreamManifest(json);
        Assert.Equal(ManifestMimeType.BTS, sm.MimeType);
        Assert.Equal("MP4A", sm.Codecs); // default
        Assert.Equal(".m4a", sm.FileExtension);
        _ = Assert.Single(sm.ChunkUrls);
        Assert.Equal("https://audio.tidal.com/file.m4a", sm.ChunkUrls[0]);
    }

    [Fact]
    public void StreamManifest_InvalidBase64_MPD_ProducesEmptyChunks_NoThrow()
    {
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = "not-base64"
        });

        StreamManifest sm = new StreamManifest(json);
        Assert.Empty(sm.ChunkUrls);
    }
}




