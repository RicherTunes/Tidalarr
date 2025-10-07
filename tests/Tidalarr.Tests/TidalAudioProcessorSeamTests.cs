using System;
using System.IO;
using System.Threading.Tasks;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalAudioProcessorSeamTests
{
    private class SuccessProcessor : IAudioProcessor
    {
        private readonly string _pathToWrite;
        public SuccessProcessor(string outputPath) { _pathToWrite = outputPath; }
        public Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, System.Threading.CancellationToken ct = default)
        {
            // Simulate ffmpeg producing the output file
            try { File.WriteAllBytes(_pathToWrite, new byte[] { 1, 2, 3 }); } catch { }
            return Task.FromResult((0, string.Empty, string.Empty));
        }
        public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments) => (0, "flac", string.Empty);
    }

    private class FailProcessor : IAudioProcessor
    {
        public Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, System.Threading.CancellationToken ct = default)
            => Task.FromResult((1, string.Empty, "error"));
        public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments) => (1, string.Empty, "not found");
    }

    [Fact]
    public async Task ProcessAudioFileAsync_UsesProcessor_ForSuccessfulFlacExtraction()
    {
        var tempDir = Path.GetTempPath();
        var input = Path.Combine(tempDir, $"tidal_in_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, new byte[] { 9, 9, 9 });
        var output = Path.ChangeExtension(input, "flac");
        try
        {
            var result = await AudioFormatHandler.ProcessAudioFileAsync(input, "FLAC", extractFlac: true, keepOriginal: false, audio: new SuccessProcessor(output));
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
        var tempDir = Path.GetTempPath();
        var input = Path.Combine(tempDir, $"tidal_in_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, new byte[] { 9, 9, 9 });
        try
        {
            var result = await AudioFormatHandler.ProcessAudioFileAsync(input, "FLAC", extractFlac: true, keepOriginal: true, audio: new FailProcessor());
            Assert.Equal(input, result);
            Assert.True(File.Exists(input));
        }
        finally
        {
            try { if (File.Exists(input)) File.Delete(input); } catch { }
        }
    }
}




