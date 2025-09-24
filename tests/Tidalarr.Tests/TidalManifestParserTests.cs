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
        var manifest = _parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Equal(".m4a", manifest.FileExtension); // mp4a codec
        Assert.True(manifest.ChunkUrls.Length >= 3); // 1 + repeats
    }

    [Fact]
    public void ParseBts_WithFields_ParsesUrlsAndEncryption()
    {
        var json = "{" +
                   "\"urls\":[\"https://a\",\"https://b\"]," +
                   "\"codecs\":\"flac\",\"mimeType\":\"audio/flac\",\"encryptionType\":\"NONE\"}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(".flac", manifest.FileExtension);
        Assert.False(manifest.IsEncrypted);
        Assert.Equal(2, manifest.ChunkUrls.Length);
    }

    [Fact]
    public void ParseBts_WithEncryptionKey_PrefersManifestToken()
    {
        var token = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        var json = "{" +
                   "\"urls\":[\"https://secure\",\"https://secure2\"]," +
                   "\"codecs\":\"mp4a.40.2\"," +
                   "\"mimeType\":\"audio/mp4\"," +
                   "\"encryptionType\":\"AES_CTR\"," +
                   "\"encryptionKey\":\"" + token + "\"," +
                   "\"keyId\":\"kid-123\"," +
                   "\"sampleRate\":48000" +
                   "}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.True(manifest.IsEncrypted);
        Assert.Equal(token, manifest.SecurityToken);
        Assert.Equal("kid-123", manifest.KeyId);
        Assert.Equal(48000, manifest.SampleRate);
    }
}
