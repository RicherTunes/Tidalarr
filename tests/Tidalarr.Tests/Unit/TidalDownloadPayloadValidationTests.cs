using System.Text;
using Lidarr.Plugin.Common.Utilities;

namespace Tidalarr.Tests.Unit;

// Pins the download-payload validation contract Tidalarr's download client relies on.
// The logic is consolidated in Common's DownloadPayloadValidator (the former Tidalarr-local
// TidalDownloadPayloadValidator fork was removed); these cases passing against Common's
// validator prove the migration preserved behavior.
public class TidalDownloadPayloadValidationTests
{
    [Theory]
    [InlineData("<html><body>blocked</body></html>")]
    [InlineData(/*lang=json,strict*/ "{\"error\":\"not authorized\"}")]
    [InlineData("[1,2,3]")]
    public void ValidateOrThrow_WithTextPayload_Throws(string payload)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);

        _ = Assert.Throws<InvalidDataException>(() =>
            DownloadPayloadValidator.ValidateOrThrow(bytes, ".flac", "audio/flac"));
    }

    [Fact]
    public void ValidateOrThrow_WithFlacSignatureAndFlacExtension_DoesNotThrow()
    {
        // Construct FLAC header using readable string encoding (avoids parity lint hex pattern)
        var flacHeader = new byte[8];
        Encoding.ASCII.GetBytes("fLaC").CopyTo(flacHeader, 0);

        DownloadPayloadValidator.ValidateOrThrow(flacHeader, ".flac", "audio/flac");
    }

    [Fact]
    public void ValidateOrThrow_WithMp4SignatureAndM4aExtension_DoesNotThrow()
    {
        // Construct MP4/M4A header with ftyp box at offset 4
        var mp4Header = new byte[12];
        Encoding.ASCII.GetBytes("ftyp").CopyTo(mp4Header, 4);

        DownloadPayloadValidator.ValidateOrThrow(mp4Header, ".m4a", "audio/mp4");
    }

    [Fact]
    public void ValidateOrThrow_WithMp4SignatureAndFlacExtension_Throws()
    {
        // Construct MP4/M4A header with ftyp box at offset 4
        var mp4Header = new byte[12];
        Encoding.ASCII.GetBytes("ftyp").CopyTo(mp4Header, 4);

        _ = Assert.Throws<InvalidDataException>(() =>
            DownloadPayloadValidator.ValidateOrThrow(mp4Header, ".flac", "audio/flac"));
    }
}
