using System;
using System.IO;
using System.Threading.Tasks;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalAudioFormatHandlerTests
{
    [Fact]
    public async Task ProcessAudioFileAsync_FLACCodec_FallsBackToCopy_WhenFfmpegMissing()
    {
        var tempDir = Path.GetTempPath();
        var input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, new byte[] { 0, 1, 2, 3, 4 });

        try
        {
            var output = await AudioFormatHandler.ProcessAudioFileAsync(input, codecs: "FLAC", extractFlac: true, keepOriginal: false);
            Assert.EndsWith(".flac", output, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(output));
        }
        finally
        {
            try { if (File.Exists(input)) File.Delete(input); } catch { }
        }
    }
}

