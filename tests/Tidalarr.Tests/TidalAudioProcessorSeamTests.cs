using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalAudioProcessorSeamTests
{
    private class SuccessProcessor(string outputPath) : IAudioProcessor
    {
        private readonly string _pathToWrite = outputPath;

        public Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, CancellationToken ct = default)
        {
            // Simulate ffmpeg producing the output file
            try { File.WriteAllBytes(this._pathToWrite, [1, 2, 3]); } catch { }
            return Task.FromResult((0, string.Empty, string.Empty));
        }
        public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments)
        {
            return (0, "flac", string.Empty);
        }
    }

    private class FailProcessor : IAudioProcessor
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

    [Fact]
    public async Task ProcessAudioFileAsync_UsesProcessor_ForSuccessfulFlacExtraction()
    {
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_in_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, [9, 9, 9]);
        string output = Path.ChangeExtension(input, "flac");
        try
        {
            string result = await AudioFormatHandler.ProcessAudioFileAsync(input, "FLAC", extractFlac: true, keepOriginal: false, audio: new SuccessProcessor(output));
            Assert.Equal(output, result);
            Assert.True(File.Exists(output));
            Assert.False(File.Exists(input));
        }
        finally
        {
            try { if (File.Exists(input)) File.Delete(input); } catch { }
            try { if (File.Exists(output)) File.Delete(output); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_ProcessorFailure_KeepsM4a()
    {
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_in_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, [9, 9, 9]);
        try
        {
            string result = await AudioFormatHandler.ProcessAudioFileAsync(input, "FLAC", extractFlac: true, keepOriginal: true, audio: new FailProcessor());
            Assert.Equal(input, result);
            Assert.True(File.Exists(input));
        }
        finally
        {
            try { if (File.Exists(input)) File.Delete(input); } catch { }
        }
    }
}




