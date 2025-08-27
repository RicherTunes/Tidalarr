using System.Text;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalManifestParserTests
{
    private readonly TidalManifestParser _parser;
    
    public TidalManifestParserTests()
    {
        _parser = new TidalManifestParser();
    }
    
    [Fact]
    public void ParseManifest_ValidDashManifest_ExtractsChunkUrls()
    {
        // Arrange
        var dashXml = CreateTestDashManifest();
        var encodedManifest = Convert.ToBase64String(Encoding.UTF8.GetBytes(dashXml));
        
        // Act
        var manifest = _parser.ParseManifest(encodedManifest, "application/dash+xml");
        
        // Assert
        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.ChunkUrls);
        Assert.Equal(".flac", manifest.FileExtension);
        Assert.Equal("application/dash+xml", manifest.MimeType);
        // Verify URLs are generated correctly from template
        Assert.All(manifest.ChunkUrls, url => Assert.Contains("audio-fa.scdn.co", url));
        Assert.All(manifest.ChunkUrls, url => Assert.EndsWith(".flac", url));
    }
    
    [Fact]
    public void ParseManifest_ValidBtsManifest_ExtractsUrls()
    {
        // Arrange
        var btsJson = CreateTestBtsManifest();
        var encodedManifest = Convert.ToBase64String(Encoding.UTF8.GetBytes(btsJson));
        
        // Act
        var manifest = _parser.ParseManifest(encodedManifest, "application/vnd.tidal.bts");
        
        // Assert
        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.ChunkUrls);
        Assert.Equal(".flac", manifest.FileExtension);
    }
    
    [Fact]
    public void ParseManifest_UnsupportedMimeType_ThrowsException()
    {
        // Arrange
        var manifest = Convert.ToBase64String(Encoding.UTF8.GetBytes("test data"));
        
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => 
            _parser.ParseManifest(manifest, "application/unknown"));
    }
    
    [Fact]
    public void ParseManifest_InvalidBase64_ThrowsException()
    {
        // Arrange
        var invalidBase64 = "not-valid-base64!@#$";
        
        // Act & Assert
        Assert.Throws<FormatException>(() => 
            _parser.ParseManifest(invalidBase64, "application/dash+xml"));
    }
    
    [Fact]
    public void ParseDashManifest_WithFlacCodec_ReturnsFlacExtension()
    {
        // Arrange
        var dashXml = @"<?xml version=""1.0""?>
        <MPD>
            <Period>
                <AdaptationSet codecs=""flac"" mimeType=""audio/flac"">
                    <SegmentTemplate media=""https://test.com/$Number$.flac"" startNumber=""1"" />
                    <SegmentTimeline>
                        <S d=""5000"" r=""9"" />
                    </SegmentTimeline>
                </AdaptationSet>
            </Period>
        </MPD>";
        
        var encodedManifest = Convert.ToBase64String(Encoding.UTF8.GetBytes(dashXml));
        
        // Act
        var manifest = _parser.ParseManifest(encodedManifest, "application/dash+xml");
        
        // Assert
        Assert.Equal(".flac", manifest.FileExtension);
        Assert.Equal("flac", manifest.Codec);
        Assert.Equal("application/dash+xml", manifest.MimeType);
    }
    
    [Fact]
    public void ParseDashManifest_WithMp4aCodec_ReturnsM4aExtension()
    {
        // Arrange
        var dashXml = @"<?xml version=""1.0""?>
        <MPD>
            <Period>
                <AdaptationSet codecs=""mp4a.40.2"" mimeType=""audio/mp4"">
                    <SegmentTemplate media=""https://test.com/$Number$.mp4"" startNumber=""1"" />
                    <SegmentTimeline>
                        <S d=""5000"" r=""9"" />
                    </SegmentTimeline>
                </AdaptationSet>
            </Period>
        </MPD>";
        
        var encodedManifest = Convert.ToBase64String(Encoding.UTF8.GetBytes(dashXml));
        
        // Act
        var manifest = _parser.ParseManifest(encodedManifest, "application/dash+xml");
        
        // Assert
        Assert.Equal(".m4a", manifest.FileExtension);
        Assert.Contains("mp4a", manifest.Codec);
    }
    
    private static string CreateTestDashManifest()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
        <MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" type=""static"" mediaPresentationDuration=""PT240S"">
            <Period start=""PT0S"">
                <AdaptationSet id=""0"" codecs=""flac"" mimeType=""audio/flac"" audioSamplingRate=""44100"">
                    <SegmentTemplate media=""https://audio-fa.scdn.co/$RepresentationID$/$Number%06d$.flac"" startNumber=""1"" />
                    <SegmentTimeline>
                        <S d=""5000"" r=""0"" />
                        <S d=""5000"" r=""0"" />
                        <S d=""5000"" r=""0"" />
                    </SegmentTimeline>
                    <Representation id=""audio_flac_44100_1411"" bandwidth=""1411000"">
                        <AudioChannelConfiguration schemeIdUri=""urn:mpeg:dash:23003:3:audio_channel_configuration:2011"" value=""2"" />
                    </Representation>
                </AdaptationSet>
            </Period>
        </MPD>";
    }
    
    private static string CreateTestBtsManifest()
    {
        return @"{
            ""urls"": [
                ""https://test.tidal.com/chunk1.flac"",
                ""https://test.tidal.com/chunk2.flac"",
                ""https://test.tidal.com/chunk3.flac""
            ],
            ""codecs"": ""flac"",
            ""mimeType"": ""audio/flac"",
            ""encryptionType"": ""NONE""
        }";
    }
}
