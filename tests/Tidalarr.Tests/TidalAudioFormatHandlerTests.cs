using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalAudioFormatHandlerTests
{
    [Fact]
    public async Task ProcessAudioFileAsync_FLACCodec_ExtractionFailure_DoesNotCreateMislabeledFlac()
    {
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);
        string output = Path.ChangeExtension(input, "flac");

        try
        {
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: false,
                audio: new FailProcessor());

            Assert.Equal(input, result);
            Assert.True(File.Exists(input));
            Assert.False(File.Exists(output));
        }
        finally
        {
            try { if (File.Exists(input)) File.Delete(input); } catch { }
            try { if (File.Exists(output)) File.Delete(output); } catch { }
        }
    }

    private sealed class FailProcessor : IAudioProcessor
    {
        public Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, CancellationToken ct = default)
        {
            return Task.FromResult((1, string.Empty, "error"));
        }

        public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments)
        {
            return (1, string.Empty, "not found");
        }
    }
}



