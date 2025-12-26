using System.IO;
using System.Text;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

public class TidalDownloadPayloadValidatorTests
{
    [Theory]
    [InlineData("<html><body>blocked</body></html>")]
    [InlineData("{\"error\":\"not authorized\"}")]
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
        byte[] bytes = [(byte)'f', (byte)'L', (byte)'a', (byte)'C', 0x00, 0x00, 0x00, 0x00];

        TidalDownloadPayloadValidator.ValidateOrThrow(bytes, ".flac", "audio/flac");
    }

    [Fact]
    public void ValidateOrThrow_WithMp4SignatureAndM4aExtension_DoesNotThrow()
    {
        byte[] bytes = [0x00, 0x00, 0x00, 0x00, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0x00, 0x00, 0x00, 0x00];

        TidalDownloadPayloadValidator.ValidateOrThrow(bytes, ".m4a", "audio/mp4");
    }

    [Fact]
    public void ValidateOrThrow_WithMp4SignatureAndFlacExtension_Throws()
    {
        byte[] bytes = [0x00, 0x00, 0x00, 0x00, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0x00, 0x00, 0x00, 0x00];

        _ = Assert.Throws<InvalidDataException>(() =>
            TidalDownloadPayloadValidator.ValidateOrThrow(bytes, ".flac", "audio/flac"));
    }
}

