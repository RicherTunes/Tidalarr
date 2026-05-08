using System.Text;
using System.Text.Json;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalStreamManifest - tests uncovered paths.
/// </summary>
public class TidalStreamManifestCovTests
{
    private static string Base64(string s)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

    [Fact]
    public void StreamManifest_UnknownMimeType_DefaultsToMPD()
    {
        // Line 41: _ => ManifestMimeType.MPD (default case)
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/unknown",
            manifest = ""
        });

        StreamManifest sm = new(json);
        Assert.Equal(ManifestMimeType.MPD, sm.MimeType);
    }

    [Fact]
    public void StreamManifest_IsEncrypted_TrueWhenSecurityTokenSet()
    {
        // Line 20: IsEncrypted => !string.IsNullOrWhiteSpace(SecurityToken)
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a",
            securityToken = "token123"
        });

        StreamManifest sm = new(json);
        Assert.True(sm.IsEncrypted);
    }

    [Fact]
    public void StreamManifest_IsEncrypted_FalseWhenSecurityTokenNull()
    {
        // Line 20: IsEncrypted => !string.IsNullOrWhiteSpace(SecurityToken)
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a"
        });

        StreamManifest sm = new(json);
        Assert.False(sm.IsEncrypted);
    }

    [Fact]
    public void StreamManifest_IsEncrypted_FalseWhenSecurityTokenEmpty()
    {
        // Line 20: IsEncrypted => !string.IsNullOrWhiteSpace(SecurityToken)
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a",
            securityToken = ""
        });

        StreamManifest sm = new(json);
        Assert.False(sm.IsEncrypted);
    }

    [Fact]
    public void StreamManifest_IsEncrypted_FalseWhenSecurityTokenWhitespace()
    {
        // Line 20: IsEncrypted => !string.IsNullOrWhiteSpace(SecurityToken)
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a",
            securityToken = "   "
        });

        StreamManifest sm = new(json);
        Assert.False(sm.IsEncrypted);
    }

    [Fact]
    public void StreamManifest_EmptyManifest_ProducesEmptyChunks()
    {
        // Line 54-64: if (!string.IsNullOrEmpty(encodedManifest)) - false branch
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = ""
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_NullManifest_ProducesEmptyChunks()
    {
        // Line 54-64: if (!string.IsNullOrEmpty(encodedManifest)) - false branch
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = (string?)null
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_MP4ACodec_ParsesCorrectly()
    {
        // Line 171: mp4a codec parsing
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number$.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Equal("MP4A", sm.Codecs);
        Assert.Equal(".m4a", sm.FileExtension);
    }

    [Fact]
    public void StreamManifest_MP4A405Codec_ParsesCorrectly()
    {
        // Line 171: mp4a.40.5 codec parsing
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a.40.5"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number$.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Equal("MP4A", sm.Codecs);
    }

    [Fact]
    public void StreamManifest_UnknownCodec_DefaultsToMP4A()
    {
        // Line 171: unknown codec defaults to MP4A
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""unknown-codec"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number$.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Equal("MP4A", sm.Codecs);
    }

    [Fact]
    public void StreamManifest_PaddedNumberFormat_ResolvedCorrectly()
    {
        // Line 143: .Replace("$Number%06d$", segmentNumber.ToString("D6"))
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number%06d$.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Contains("000001", sm.ChunkUrls[0]);
    }

    [Fact]
    public void StreamManifest_CustomStartNumber_StartsAtCorrectNumber()
    {
        // Line 111: uint startNumber parsing
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number$.m4a"" startNumber=""5"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Contains("/seg_5.m4a", sm.ChunkUrls[0]);
    }

    [Fact]
    public void StreamManifest_NoSegmentTimeline_ProducesEmptyChunks()
    {
        // Line 127-148: segmentTimeline null branch
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number$.m4a"" startNumber=""1"">
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_NoInitializationTemplate_SkipsInit()
    {
        // Line 118-123: initializationTemplate null/empty branch
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number$.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        // Should only have segment URL, no init
        Assert.Single(sm.ChunkUrls);
        Assert.Contains("seg_1.m4a", sm.ChunkUrls[0]);
    }

    [Fact]
    public void StreamManifest_NoSegmentTemplate_ProducesEmptyChunks()
    {
        // Line 105: segmentTemplate null branch
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_NoRepresentation_ProducesEmptyChunks()
    {
        // Line 93: representation null branch
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_InvalidXml_ProducesEmptyChunks()
    {
        // Line 155-158: catch exception in ParseDashManifest
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64("not valid xml <")
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_MissingManifestProperty_ProducesEmptyChunks()
    {
        // Line 66-70: catch exception in ParseStreamData
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml"
            // manifest property missing - will throw on GetProperty
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_KeyIdNull_DefaultsToEmpty()
    {
        // Line 47: KeyId = keyIdElement.GetString() ?? string.Empty
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a",
            keyId = (string?)null
        });

        StreamManifest sm = new(json);
        Assert.Equal(string.Empty, sm.KeyId);
    }

    [Fact]
    public void StreamManifest_MultipleSegmentsWithRepeat_GeneratesCorrectCount()
    {
        // Line 134-146: repeat handling with multiple segments
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/seg_$Number$.m4a"" initialization=""https://cdn.tidal.com/audio/init.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""4"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        // r=4 means 1 + 4 = 5 segments, plus 1 init = 6 total
        Assert.Equal(6, sm.ChunkUrls.Length);
    }

    [Fact]
    public void StreamManifest_RepresentationId_ReplacedInUrls()
    {
        // Line 120-122: $RepresentationID$ replacement
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""my_rep_id"" codecs=""mp4a"">
        <SegmentTemplate media=""https://cdn.tidal.com/audio/$RepresentationID$/seg_$Number$.m4a"" initialization=""https://cdn.tidal.com/audio/$RepresentationID$/init.m4a"" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Contains("my_rep_id", sm.ChunkUrls[0]);
        Assert.Contains("my_rep_id", sm.ChunkUrls[1]);
    }

    [Fact]
    public void StreamManifest_EmptyMediaTemplate_ProducesEmptyChunks()
    {
        // Line 113: if (!string.IsNullOrEmpty(mediaTemplate)) - false branch
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
  <Period>
    <AdaptationSet>
      <Representation id=""audio_0"" codecs=""mp4a"">
        <SegmentTemplate media="""" startNumber=""1"">
          <SegmentTimeline>
            <S r=""0"" />
          </SegmentTimeline>
        </SegmentTemplate>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/dash+xml",
            manifest = Base64(xml)
        });

        StreamManifest sm = new(json);
        Assert.Empty(sm.ChunkUrls);
    }

    [Fact]
    public void StreamManifest_SecurityToken_NullByDefault()
    {
        // Line 19: SecurityToken defaults to null
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a"
        });

        StreamManifest sm = new(json);
        Assert.Null(sm.SecurityToken);
    }

    [Fact]
    public void StreamManifest_SecurityToken_SetWhenProvided()
    {
        // Line 51: SecurityToken = tokenElement.GetString()
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = "application/vnd.tidal.bts",
            manifest = "https://audio.tidal.com/file.m4a",
            securityToken = "my-token-value"
        });

        StreamManifest sm = new(json);
        Assert.Equal("my-token-value", sm.SecurityToken);
    }
}
