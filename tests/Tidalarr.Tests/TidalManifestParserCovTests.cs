using System.Text;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalManifestParserCovTests
{
    private readonly TidalManifestParser _parser = new();

    // Line 20: throw new NotSupportedException($"Unsupported manifest type: {mimeType}")
    [Fact]
    public void ParseManifest_UnsupportedMimeType_ThrowsNotSupportedException()
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("any content"));

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => _parser.ParseManifest(encoded, "application/unknown"));

        Assert.Contains("Unsupported manifest type: application/unknown", ex.Message);
    }

    // Line 25: throw new FormatException("Invalid base64 manifest encoding")
    [Fact]
    public void ParseManifest_InvalidBase64_ThrowsFormatException()
    {
        FormatException ex = Assert.Throws<FormatException>(
            () => _parser.ParseManifest("not-valid-base64!!!", "application/dash+xml"));

        Assert.Equal("Invalid base64 manifest encoding", ex.Message);
    }

    // Line 34: throw new InvalidOperationException("No AdaptationSet found in DASH manifest")
    [Fact]
    public void ParseDash_NoAdaptationSet_ThrowsInvalidOperationException()
    {
        string xml = @"<MPD><Period></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => _parser.ParseManifest(encoded, "application/dash+xml"));

        Assert.Equal("No AdaptationSet found in DASH manifest", ex.Message);
    }

    // Lines 65-68: throw new InvalidOperationException("No URLs found in BTS manifest") - no urls property
    [Fact]
    public void ParseBts_NoUrlsProperty_ThrowsInvalidOperationException()
    {
        string json = @"{""codecs"":""mp4a"",""mimeType"":""audio/mp4""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => _parser.ParseManifest(encoded, "application/vnd.tidal.bts"));

        Assert.Equal("No URLs found in BTS manifest", ex.Message);
    }

    // Lines 75-78: throw new InvalidOperationException("No URLs found in BTS manifest") - empty urls
    [Fact]
    public void ParseBts_EmptyUrlsArray_ThrowsInvalidOperationException()
    {
        string json = @"{""urls"":[],""codecs"":""mp4a""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => _parser.ParseManifest(encoded, "application/vnd.tidal.bts"));

        Assert.Equal("No URLs found in BTS manifest", ex.Message);
    }

    // Lines 141-143: sample rate as string parsing
    [Fact]
    public void ParseBts_SampleRateAsString_ParsesCorrectly()
    {
        string json = @"{""urls"":[""https://test""],""sampleRate"":""96000""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(96000, manifest.SampleRate);
    }

    // Line 147: default sample rate 44100
    [Fact]
    public void ParseBts_NoSampleRate_DefaultsTo44100()
    {
        string json = @"{""urls"":[""https://test""]}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(44100, manifest.SampleRate);
    }

    // Line 39: codec from AdaptationSet when Representation has no codecs
    [Fact]
    public void ParseDash_CodecFromAdaptationSet_WhenRepresentationHasNoCodecs()
    {
        string xml = @"<MPD><Period><AdaptationSet codecs='flac' audioSamplingRate='48000'>
          <Representation id='test'>
            <SegmentTemplate media='https://test.com/seg_$Number$.m4s'>
              <SegmentTimeline><S d='1'/></SegmentTimeline>
            </SegmentTemplate>
          </Representation>
        </AdaptationSet></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Equal("FLAC", manifest.Codec);
    }

    // Line 40: codec fallback "unknown"
    [Fact]
    public void ParseDash_NoCodecAttribute_DefaultsToUnknown()
    {
        string xml = @"<MPD><Period><AdaptationSet audioSamplingRate='48000'>
          <Representation id='test'>
            <SegmentTemplate media='https://test.com/seg_$Number$.m4s'>
              <SegmentTimeline><S d='1'/></SegmentTimeline>
            </SegmentTemplate>
          </Representation>
        </AdaptationSet></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Equal("unknown", manifest.Codec);
    }

    // Lines 120-127: drmSecurityToken extraction
    [Fact]
    public void ParseBts_DrmSecurityToken_ExtractedCorrectly()
    {
        string json = @"{""urls"":[""https://test""],""drmSecurityToken"":""drm-token-123""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal("drm-token-123", manifest.SecurityToken);
        Assert.True(manifest.IsEncrypted);
    }

    // Lines 214-216: DetermineFileExtension for mp4a codec
    [Fact]
    public void ParseBts_Mp4aCodec_ReturnsM4aExtension()
    {
        string json = @"{""urls"":[""https://test""],""codecs"":""mp4a.40.2""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(".m4a", manifest.FileExtension);
    }

    // Lines 221-223: DetermineFileExtension from URL .flac
    [Fact]
    public void ParseBts_UrlContainsFlac_ReturnsFlacExtension()
    {
        string json = @"{""urls"":[""https://test.com/audio.flac""],""codecs"":""unknown""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(".flac", manifest.FileExtension);
    }

    // Lines 225-229: DetermineFileExtension from URL .mp4
    [Fact]
    public void ParseBts_UrlContainsMp4_ReturnsM4aExtension()
    {
        string json = @"{""urls"":[""https://test.com/audio.mp4""],""codecs"":""unknown""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(".m4a", manifest.FileExtension);
    }

    // Lines 230-234: DetermineFileExtension from URL .ts
    [Fact]
    public void ParseBts_UrlContainsTs_ReturnsTsExtension()
    {
        string json = @"{""urls"":[""https://test.com/audio.ts""],""codecs"":""unknown""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(".ts", manifest.FileExtension);
    }

    // Line 236: DetermineFileExtension default .m4a
    [Fact]
    public void ParseBts_UnknownCodecAndUrl_ReturnsDefaultM4aExtension()
    {
        string json = @"{""urls"":[""https://test.com/audio""],""codecs"":""unknown""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(".m4a", manifest.FileExtension);
    }

    // Lines 177-186: No SegmentTimeline - uses single URL
    [Fact]
    public void ParseDash_NoSegmentTimeline_GeneratesSingleUrl()
    {
        string xml = @"<MPD><Period><AdaptationSet codecs='mp4a.40.2'>
          <Representation id='rep1'>
            <SegmentTemplate media='https://test.com/seg_$Number$.m4s' />
          </Representation>
        </AdaptationSet></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Single(manifest.ChunkUrls);
        Assert.Contains("seg_1.m4s", manifest.ChunkUrls[0]);
    }

    // Line 42: DASH default sample rate 44100 when not specified
    [Fact]
    public void ParseDash_NoSampleRate_DefaultsTo44100()
    {
        string xml = @"<MPD><Period><AdaptationSet codecs='mp4a.40.2'>
          <Representation id='test'>
            <SegmentTemplate media='https://test.com/seg_$Number$.m4s'>
              <SegmentTimeline><S d='1'/></SegmentTimeline>
            </SegmentTemplate>
          </Representation>
        </AdaptationSet></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Equal(44100, manifest.SampleRate);
    }

    // Line 243: NormalizeCodec default trim path (neither flac nor mp4a)
    [Fact]
    public void ParseDash_OtherCodec_ReturnsTrimmedCodec()
    {
        string xml = @"<MPD><Period><AdaptationSet codecs='  AAC  '>
          <Representation id='test'>
            <SegmentTemplate media='https://test.com/seg_$Number$.m4s'>
              <SegmentTimeline><S d='1'/></SegmentTimeline>
            </SegmentTemplate>
          </Representation>
        </AdaptationSet></Period></MPD>";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/dash+xml");

        Assert.Equal("AAC", manifest.Codec);
    }

    // Lines 100-109: securityToken extraction
    [Fact]
    public void ParseBts_SecurityToken_ExtractedCorrectly()
    {
        string json = @"{""urls"":[""https://test""],""securityToken"":""sec-token-xyz""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal("sec-token-xyz", manifest.SecurityToken);
        Assert.True(manifest.IsEncrypted);
    }

    // Lines 111-118: encryptionKey as token fallback
    [Fact]
    public void ParseBts_EncryptionKeyAsSecurityToken_WhenNoSecurityToken()
    {
        string json = @"{""urls"":[""https://test""],""encryptionKey"":""enc-key-456""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal("enc-key-456", manifest.SecurityToken);
        Assert.True(manifest.IsEncrypted);
    }

    // Line 87: encryption via securityToken presence
    [Fact]
    public void ParseBts_SecurityTokenPresence_MarksEncrypted()
    {
        string json = @"{""urls"":[""https://test""],""encryptionType"":""NONE"",""securityToken"":""token""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.True(manifest.IsEncrypted);
    }

    // Lines 70-73: null/empty strings filtered from urls
    [Fact]
    public void ParseBts_NullStringsInUrls_FilteredOut()
    {
        string json = @"{""urls"":[""https://valid"","""",""https://valid2""],""codecs"":""mp4a""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal(2, manifest.ChunkUrls.Length);
    }

    // Lines 81-82: mimeType default
    [Fact]
    public void ParseBts_NoMimeType_DefaultsToAudioUnknown()
    {
        string json = @"{""urls"":[""https://test""],""codecs"":""mp4a""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal("audio/unknown", manifest.MimeType);
    }

    // Lines 80: codec default
    [Fact]
    public void ParseBts_NoCodec_DefaultsToUnknown()
    {
        string json = @"{""urls"":[""https://test""],""mimeType"":""audio/mp4""}";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Core.Models.TidalManifest manifest = _parser.ParseManifest(encoded, "application/vnd.tidal.bts");

        Assert.Equal("unknown", manifest.Codec);
    }
}
