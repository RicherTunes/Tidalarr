using System.Text;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalManifestParserTests
{
    private readonly TidalManifestParser _parser = new();

    [Fact]
    public void ParseDash_WithSegmentTimelineRepeats_GeneratesMultipleUrls()
    {
        string xml = @"<MPD><Period><AdaptationSet codecs='mp4a.40.2' audioSamplingRate='48000'>
          <SegmentTemplate media='https://test.com/chunk_$Number%06d$.m4s'>
            <SegmentTimeline>
              <S d='2' r='2'/>
            </SegmentTimeline>
          </SegmentTemplate>
        </AdaptationSet></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
        Core.Models.TidalManifest manifest = this._parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Equal(".m4a", manifest.FileExtension); // mp4a codec
        Assert.Equal("MP4A", manifest.Codec);
        Assert.True(manifest.ChunkUrls.Length >= 3); // 1 + repeats
    }

    [Fact]
    public void ParseDash_WithRepresentationCodec_Flac_UsesM4aContainerExtension()
    {
        string xml = @"<MPD xmlns='urn:mpeg:dash:schema:mpd:2011'><Period><AdaptationSet>
          <Representation id='audio_flac' codecs='flac'>
            <SegmentTemplate initialization='https://test.com/init.m4a' media='https://test.com/seg_$Number%06d$.m4a' startNumber='1'>
              <SegmentTimeline><S r='0' /></SegmentTimeline>
            </SegmentTemplate>
          </Representation>
        </AdaptationSet></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
        Core.Models.TidalManifest manifest = this._parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Equal("FLAC", manifest.Codec);
        Assert.Equal(".m4a", manifest.FileExtension);
        Assert.True(manifest.ChunkUrls.Length >= 2); // init + segment
    }

    [Fact]
    public void ParseBts_WithFields_ParsesUrlsAndEncryption()
    {
        string json = "{" +
                   "\"urls\":[\"https://a\",\"https://b\"]," +
                   "\"codecs\":\"flac\",\"mimeType\":\"audio/flac\",\"encryptionType\":\"NONE\"}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        Core.Models.TidalManifest manifest = this._parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(".flac", manifest.FileExtension);
        Assert.False(manifest.IsEncrypted);
        Assert.Equal(2, manifest.ChunkUrls.Length);
    }

    [Fact]
    public void ParseBts_WithEncryptionKey_PrefersManifestToken()
    {
        string token = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        string json = "{" +
                   "\"urls\":[\"https://secure\",\"https://secure2\"]," +
                   "\"codecs\":\"mp4a.40.2\"," +
                   "\"mimeType\":\"audio/mp4\"," +
                   "\"encryptionType\":\"AES_CTR\"," +
                   "\"encryptionKey\":\"" + token + "\"," +
                   "\"keyId\":\"kid-123\"," +
                   "\"sampleRate\":48000" +
                   "}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        Core.Models.TidalManifest manifest = this._parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.True(manifest.IsEncrypted);
        Assert.Equal(token, manifest.SecurityToken);
        Assert.Equal("kid-123", manifest.KeyId);
        Assert.Equal(48000, manifest.SampleRate);
    }
}


