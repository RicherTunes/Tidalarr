using System;
using System.Text;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalManifestParserTests
{
    private readonly TidalManifestParser _parser = new();

    [Fact]
    public void ParseDash_WithSegmentTimelineRepeats_GeneratesMultipleUrls()
    {
        var xml = @"<MPD><Period><AdaptationSet codecs='mp4a.40.2' audioSamplingRate='48000'>
          <SegmentTemplate media='https://test.com/chunk_$Number%06d$.m4s'>
            <SegmentTimeline>
              <S d='2' r='2'/>
            </SegmentTimeline>
          </SegmentTemplate>
        </AdaptationSet></Period></MPD>";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
        var m = _parser.ParseManifest(encoded, "application/dash+xml");
        Assert.Equal(".m4a", m.FileExtension); // mp4a codec
        Assert.True(m.ChunkUrls.Length >= 3); // 1 + repeats
    }

    [Fact]
    public void ParseBts_WithFields_ParsesUrlsAndEncryption()
    {
        var json = "{" +
                   "\"urls\":[\"https://a\",\"https://b\"]," +
                   "\"codecs\":\"flac\",\"mimeType\":\"audio/flac\",\"encryptionType\":\"NONE\"}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var m = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");
        Assert.Equal(".flac", m.FileExtension);
        Assert.False(m.IsEncrypted);
        Assert.Equal(2, m.ChunkUrls.Length);
    }
}

