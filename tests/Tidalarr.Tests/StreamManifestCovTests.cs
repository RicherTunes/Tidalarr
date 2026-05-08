using System.Text.Json;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for StreamManifest class.
/// Source: src/Tidalarr/Domain/Streaming/TidalStreamManifest.cs
/// </summary>
public class StreamManifestCovTests
{
    #region ManifestMimeType Enum Tests

    [Fact]
    public void ManifestMimeType_HasTwoValues()
    {
        // Source lines 7-10: enum ManifestMimeType with MPD and BTS
        var values = Enum.GetValues<ManifestMimeType>();
        Assert.Equal(2, values.Length);
        Assert.Equal(0, (int)ManifestMimeType.MPD);
        Assert.Equal(1, (int)ManifestMimeType.BTS);
    }

    #endregion

    #region Constructor and Property Tests

    [Fact]
    public void Constructor_WithMpdManifest_ParsesCorrectly()
    {
        // Source lines 24-27: Constructor calls ParseStreamData
        // Source lines 37-41: MimeType switch for "application/dash+xml"
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""test1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""chunk$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 39: MimeType = ManifestMimeType.MPD for "application/dash+xml"
        Assert.Equal(ManifestMimeType.MPD, manifest.MimeType);
        // Source line 15: ChunkUrls populated from template
        Assert.NotEmpty(manifest.ChunkUrls);
        // Source line 16: FileExtension defaults to .m4a
        Assert.Equal(".m4a", manifest.FileExtension);
        // Source line 17: Codecs parsed from representation
        Assert.Equal("MP4A", manifest.Codecs);
    }

    [Fact]
    public void Constructor_WithBtsManifest_SetsMimeTypeAndChunkUrl()
    {
        // Source lines 37-41: MimeType switch for "application/vnd.tidal.bts"
        var json = "{\"manifestMimeType\":\"application/vnd.tidal.bts\",\"manifest\":\"https://example.com/stream.bts\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 40: MimeType = ManifestMimeType.BTS
        Assert.Equal(ManifestMimeType.BTS, manifest.MimeType);
        // Source line 164: ChunkUrls = [encodedManifest] for BTS
        Assert.Single(manifest.ChunkUrls);
        Assert.Equal("https://example.com/stream.bts", manifest.ChunkUrls[0]);
        // Source lines 165-166: FileExtension and Codecs for BTS
        Assert.Equal(".m4a", manifest.FileExtension);
        Assert.Equal("MP4A", manifest.Codecs);
    }

    [Fact]
    public void Constructor_WithUnknownMimeType_DefaultsToMpd()
    {
        // Source line 41: _ => ManifestMimeType.MPD (default case)
        var json = "{\"manifestMimeType\":\"application/unknown\",\"manifest\":\"\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Equal(ManifestMimeType.MPD, manifest.MimeType);
    }

    [Fact]
    public void Constructor_WithKeyIdAndSecurityToken_SetsEncryptionProperties()
    {
        // Source lines 45-52: keyId and securityToken extraction
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"\",\"keyId\":\"test-key-123\",\"securityToken\":\"token-abc\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 47: KeyId from keyId property
        Assert.Equal("test-key-123", manifest.KeyId);
        // Source line 51: SecurityToken from securityToken property
        Assert.Equal("token-abc", manifest.SecurityToken);
        // Source line 20: IsEncrypted = !string.IsNullOrWhiteSpace(SecurityToken)
        Assert.True(manifest.IsEncrypted);
    }

    [Fact]
    public void Constructor_WithoutSecurityToken_IsNotEncrypted()
    {
        // Source line 20: IsEncrypted is false when SecurityToken is null/empty
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"\",\"keyId\":\"test-key-123\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.False(manifest.IsEncrypted);
    }

    [Fact]
    public void Constructor_WithNullKeyId_SetsEmptyString()
    {
        // Source line 47: KeyId = GetString() ?? string.Empty
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"\",\"keyId\":null}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Equal(string.Empty, manifest.KeyId);
    }

    [Fact]
    public void Constructor_WithNullSecurityToken_SetsNull()
    {
        // Source line 51: SecurityToken = GetString() (can be null)
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"\",\"securityToken\":null}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Null(manifest.SecurityToken);
        Assert.False(manifest.IsEncrypted);
    }

    #endregion

    #region ParseDashManifest Tests

    [Fact]
    public void ParseDashManifest_WithInitializationTemplate_AddsInitializationUrl()
    {
        // Source lines 118-123: initializationTemplate processing
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""rep1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""chunk$Number$.m4s"" initialization=""init$RepresentationID$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 120: initialization URL added first with representation ID replaced
        Assert.Contains("initrep1.m4s", manifest.ChunkUrls);
        // Source line 125: segment URLs follow
        Assert.Contains("chunk1.m4s", manifest.ChunkUrls);
    }

    [Fact]
    public void ParseDashManifest_WithSegmentTimelineRepeat_GeneratesMultipleSegments()
    {
        // Source lines 134-146: segment timeline with repeat (r attribute)
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline>
                        <S d=""1000000"" r=""2""/>
                    </SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 134: repeat = int.TryParse(s.Attribute("r")?.Value, out int r) ? r : 0
        // Source line 135: segmentCount = 1 + repeat (1 occurrence + r repeats)
        // 1 + 2 = 3 segments
        Assert.Equal(3, manifest.ChunkUrls.Length);
        Assert.Equal("seg1.m4s", manifest.ChunkUrls[0]);
        Assert.Equal("seg2.m4s", manifest.ChunkUrls[1]);
        Assert.Equal("seg3.m4s", manifest.ChunkUrls[2]);
    }

    [Fact]
    public void ParseDashManifest_WithCustomStartNumber_UsesCorrectStartNumber()
    {
        // Source line 111: startNumber parsing
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""5"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 129: segmentNumber starts at startNumber (5)
        Assert.Single(manifest.ChunkUrls);
        Assert.Equal("seg5.m4s", manifest.ChunkUrls[0]);
    }

    [Fact]
    public void ParseDashManifest_WithPaddedNumberTemplate_ReplacesCorrectly()
    {
        // Source line 143: $Number%06d$ replacement with D6 format
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""seg$Number%06d$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 143: segmentNumber.ToString("D6") for padded format
        Assert.Single(manifest.ChunkUrls);
        Assert.Equal("seg000001.m4s", manifest.ChunkUrls[0]);
    }

    [Fact]
    public void ParseDashManifest_WithFlacCodec_SetsCodecsAndExtension()
    {
        // Source lines 96-98: ParseCodecs and DetermineFileExtension for flac
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""flac"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 171: ParseCodecs returns "FLAC" for flac
        Assert.Equal("FLAC", manifest.Codecs);
        // Source line 180: DetermineFileExtension returns ".m4a" for flac (FLAC in M4A container)
        Assert.Equal(".m4a", manifest.FileExtension);
    }

    [Fact]
    public void ParseDashManifest_WithMp4a405Codec_SetsMp4aCodecs()
    {
        // Source line 171: mp4a.40.5 -> MP4A
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Equal("MP4A", manifest.Codecs);
    }

    [Fact]
    public void ParseDashManifest_WithGenericMp4aCodec_SetsMp4aCodecs()
    {
        // Source line 171: mp4a -> MP4A
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Equal("MP4A", manifest.Codecs);
    }

    [Fact]
    public void ParseDashManifest_WithUnknownCodec_DefaultsToMp4a()
    {
        // Source line 171: unknown codec -> MP4A (default)
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""unknown-codec"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Equal("MP4A", manifest.Codecs);
        Assert.Equal(".m4a", manifest.FileExtension);
    }

    [Fact]
    public void ParseDashManifest_WithNoCodecsAttribute_UsesDefaults()
    {
        // Source line 96: codecsAttr defaults to "" when missing
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source lines 171, 184: defaults to MP4A and .m4a
        Assert.Equal("MP4A", manifest.Codecs);
        Assert.Equal(".m4a", manifest.FileExtension);
    }

    [Fact]
    public void ParseDashManifest_WithRepresentationIdReplacement_ReplacesCorrectly()
    {
        // Source lines 101, 121, 141: $RepresentationID$ replacement
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""myRepId"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""chunk_$RepresentationID$_$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline><S d=""1000000""/></SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 141: $RepresentationID$ replaced with representation id
        Assert.Contains("chunk_myRepId_1.m4s", manifest.ChunkUrls[0]);
    }

    #endregion

    #region Exception/Fallback Tests

    [Fact]
    public void Constructor_WithInvalidBase64_FallsBackToEmptyChunkUrls()
    {
        // Source lines 65-70: catch block sets ChunkUrls to empty
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"not-valid-base64!!!\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 69: Fallback to empty manifest
        Assert.Empty(manifest.ChunkUrls);
    }

    [Fact]
    public void Constructor_WithInvalidDashXml_FallsBackToEmptyChunkUrls()
    {
        // Source lines 155-158: catch in ParseDashManifest
        var invalidXml = "<not><valid>dash</xml>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(invalidXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Source line 157: ChunkUrls = [] on exception
        Assert.Empty(manifest.ChunkUrls);
    }

    [Fact]
    public void Constructor_WithEmptyManifest_SkipsParsing()
    {
        // Source line 54: if (!string.IsNullOrEmpty(encodedManifest))
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Empty manifest doesn't parse, ChunkUrls remains default
        Assert.Empty(manifest.ChunkUrls);
    }

    [Fact]
    public void Constructor_WithNullManifest_SkipsParsing()
    {
        // Source line 54: null check skips manifest parsing
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":null}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Empty(manifest.ChunkUrls);
    }

    [Fact]
    public void Constructor_WithMissingManifestProperty_FallsBackToEmpty()
    {
        // Source line 35: TryGetProperty would fail, caught by outer try/catch
        var json = "{\"manifestMimeType\":\"application/dash+xml\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Missing "manifest" property causes JsonException, caught at line 66
        Assert.Empty(manifest.ChunkUrls);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_WithNoSegmentTimeline_GeneratesEmptyUrls()
    {
        // Source line 126: segmentTimeline null check
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // No SegmentTimeline means no segments generated
        Assert.Empty(manifest.ChunkUrls);
    }

    [Fact]
    public void Constructor_WithNoSegmentTemplate_GeneratesEmptyUrls()
    {
        // Source line 104: segmentTemplate null check
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a.40.5"">
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Empty(manifest.ChunkUrls);
    }

    [Fact]
    public void Constructor_WithNoRepresentation_GeneratesEmptyUrls()
    {
        // Source lines 90-93: representation null check
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        Assert.Empty(manifest.ChunkUrls);
    }

    [Fact]
    public void Constructor_WithMultipleTimelineEntries_GeneratesAllSegments()
    {
        // Multiple <S> elements in timeline
        var dashXml = @"<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"">
            <Period><AdaptationSet><Representation id=""r1"" codecs=""mp4a.40.5"">
                <SegmentTemplate media=""seg$Number$.m4s"" startNumber=""1"">
                    <SegmentTimeline>
                        <S d=""1000000""/>
                        <S d=""1000000"" r=""1""/>
                        <S d=""1000000""/>
                    </SegmentTimeline>
                </SegmentTemplate>
            </Representation></AdaptationSet></Period>
        </MPD>";
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dashXml));
        var json = $"{{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"{encodedManifest}\"}}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // 1 + (1+1) + 1 = 4 segments
        Assert.Equal(4, manifest.ChunkUrls.Length);
        Assert.Equal("seg1.m4s", manifest.ChunkUrls[0]);
        Assert.Equal("seg2.m4s", manifest.ChunkUrls[1]);
        Assert.Equal("seg3.m4s", manifest.ChunkUrls[2]);
        Assert.Equal("seg4.m4s", manifest.ChunkUrls[3]);
    }

    [Fact]
    public void Constructor_WithWhitespaceSecurityToken_IsNotEncrypted()
    {
        // Source line 20: IsEncrypted uses IsNullOrWhiteSpace
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"\",\"securityToken\":\"   \"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Whitespace-only token should NOT be encrypted (IsNullOrWhiteSpace returns true for whitespace)
        Assert.False(manifest.IsEncrypted);
    }

    [Fact]
    public void Constructor_DefaultPropertyValues_AreCorrect()
    {
        // Source lines 15-23: Default property values
        var json = "{\"manifestMimeType\":\"application/dash+xml\",\"manifest\":\"\"}";
        using var doc = JsonDocument.Parse(json);
        var manifest = new StreamManifest(doc.RootElement);

        // Default ChunkUrls is empty array (line 15)
        Assert.Empty(manifest.ChunkUrls);
        // Default FileExtension is .m4a (line 16)
        Assert.Equal(".m4a", manifest.FileExtension);
        // Default Codecs is MP4A (line 17)
        Assert.Equal("MP4A", manifest.Codecs);
        // Default KeyId is empty string (line 18)
        Assert.Equal(string.Empty, manifest.KeyId);
        // Default SecurityToken is null (line 19)
        Assert.Null(manifest.SecurityToken);
    }

    #endregion
}
