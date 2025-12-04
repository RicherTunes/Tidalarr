using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalAudioFormatHandlerTests
{
    [Fact]
    public async Task ProcessAudioFileAsync_FLACCodec_FallsBackToCopy_WhenFfmpegMissing()
    {
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        try
        {
            string output = await AudioFormatHandler.ProcessAudioFileAsync(input, codecs: "FLAC", extractFlac: true, keepOriginal: false);
            Assert.EndsWith(".flac", output, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(output));
        }
        finally
        {
            try { if (File.Exists(input)) File.Delete(input); } catch { }
        }
    }
}




