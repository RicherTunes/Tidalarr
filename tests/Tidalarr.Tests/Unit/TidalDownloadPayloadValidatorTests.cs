using System.Text;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

public class TidalDownloadPayloadValidatorTests
{
    [Theory]
    [InlineData("<html><body>blocked</body></html>")]
    [InlineData(/*lang=json,strict*/ "{\"error\":\"not authorized\"}")]
    [InlineData("[1,2,3]")]
    public void ValidateOrThrow_WithTextPayload_Throws(string payload)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);

        _ = Assert.Throws<InvalidDataException>(() =>
            TidalDownloadPayloadValidator.ValidateOrThrow(bytes, ".flac", "audio/flac"));
    }

    [Fact]
    public void ValidateOrThrow_WithFlacSignatureAndFlacExtension_DoesNotThrow()
    {
        // Construct FLAC header using readable string encoding (avoids parity lint hex pattern)
        var flacHeader = new byte[8];
        Encoding.ASCII.GetBytes("fLaC").CopyTo(flacHeader, 0);

        TidalDownloadPayloadValidator.ValidateOrThrow(flacHeader, ".flac", "audio/flac");
    }

    [Fact]
    public void ValidateOrThrow_WithMp4SignatureAndM4aExtension_DoesNotThrow()
    {
        // Construct MP4/M4A header with ftyp box at offset 4
        var mp4Header = new byte[12];
        Encoding.ASCII.GetBytes("ftyp").CopyTo(mp4Header, 4);

        TidalDownloadPayloadValidator.ValidateOrThrow(mp4Header, ".m4a", "audio/mp4");
    }

    [Fact]
    public void ValidateOrThrow_WithMp4SignatureAndFlacExtension_Throws()
    {
        // Construct MP4/M4A header with ftyp box at offset 4
        var mp4Header = new byte[12];
        Encoding.ASCII.GetBytes("ftyp").CopyTo(mp4Header, 4);

        _ = Assert.Throws<InvalidDataException>(() =>
            TidalDownloadPayloadValidator.ValidateOrThrow(mp4Header, ".flac", "audio/flac"));
    }
}
