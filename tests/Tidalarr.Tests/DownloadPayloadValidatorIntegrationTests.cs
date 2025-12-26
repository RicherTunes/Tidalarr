using System.IO;
using Lidarr.Plugin.Common.Utilities;
using Xunit;

namespace Tidalarr.Tests;

/// <summary>
/// Tests validating Tidalarr's usage of the common DownloadPayloadValidator.
/// Ensures the shared validator behavior matches Tidalarr's download requirements.
/// </summary>
public class DownloadPayloadValidatorIntegrationTests
{
    [Fact]
    public void ValidateOrThrow_ShortM4aBuffer_ThrowsSpecificMp4Error()
    {
        // Arrange: 4-byte buffer is too short for M4A validation (requires 8 bytes for ftyp at offset 4)
        var shortBuffer = new byte[] { 0x00, 0x00, 0x00, 0x14 };

        // Act & Assert: Should throw with specific MP4/M4A message, not generic error
        var ex = Assert.Throws<InvalidDataException>(() =>
            DownloadPayloadValidator.ValidateOrThrow(shortBuffer.AsSpan(), "m4a"));

        Assert.Contains("MP4/M4A", ex.Message);
        Assert.Contains("8 bytes", ex.Message);
        Assert.Contains("4", ex.Message); // got 4 bytes
    }

    [Fact]
    public void ValidateOrThrow_EmptyM4aBuffer_ThrowsSpecificMp4Error()
    {
        // Arrange: Empty buffer for M4A
        var emptyBuffer = Array.Empty<byte>();

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            DownloadPayloadValidator.ValidateOrThrow(emptyBuffer.AsSpan(), ".m4a"));

        Assert.Contains("MP4/M4A", ex.Message);
    }

    [Fact]
    public void ValidateOrThrow_ValidFlacHeader_Succeeds()
    {
        // Arrange: Valid FLAC magic bytes
        var flacHeader = new byte[] { 0x66, 0x4C, 0x61, 0x43, 0x00, 0x00, 0x00, 0x22 }; // "fLaC"

        // Act & Assert: Should not throw
        DownloadPayloadValidator.ValidateOrThrow(flacHeader.AsSpan(), "flac");
    }

    [Fact]
    public void ValidateOrThrow_HtmlPayload_ThrowsNonAudioError()
    {
        // Arrange: HTML error page (common Tidal failure mode)
        var htmlPayload = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>Error</body></html>");

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            DownloadPayloadValidator.ValidateOrThrow(htmlPayload.AsSpan(), "flac"));

        Assert.Contains("non-audio", ex.Message.ToLowerInvariant());
    }
}
