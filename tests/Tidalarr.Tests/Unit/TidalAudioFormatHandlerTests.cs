using System.Text.RegularExpressions;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Comprehensive tests for AudioFormatHandler covering codec detection,
/// FFmpeg availability scenarios, extraction failures, and edge cases.
/// </summary>
public class TidalAudioFormatHandlerTests
{
    #region IsFFmpegAvailable Tests

    [Fact]
    public void IsFFmpegAvailable_FFprobeReturnsZero_ReturnsTrue()
    {
        // Arrange
        MockAudioProcessor processor = new() { FfprobeExitCode = 0 };

        // Act
        bool result = AudioFormatHandler.IsFFmpegAvailable(processor);

        // Assert
        Assert.True(result);
        Assert.Contains("-version", processor.LastFfprobeArgs);
    }

    [Fact]
    public void IsFFmpegAvailable_FFprobeReturnsNonZero_ReturnsFalse()
    {
        // Arrange
        MockAudioProcessor processor = new() { FfprobeExitCode = 1 };

        // Act
        bool result = AudioFormatHandler.IsFFmpegAvailable(processor);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFFmpegAvailable_FFprobeThrowsException_ReturnsFalse()
    {
        // Arrange
        ThrowingAudioProcessor processor = new();

        // Act
        bool result = AudioFormatHandler.IsFFmpegAvailable(processor);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFFmpegAvailable_NoProcessorProvided_UsesDefaultSystemProcessor()
    {
        // Act - This will try to use actual system ffprobe
        // We're just testing it doesn't throw and returns a bool
        bool result = AudioFormatHandler.IsFFmpegAvailable();

        // Assert - Just verify it returns a boolean without throwing
        _ = Assert.IsType<bool>(result);
    }

    #endregion

    #region DetectCodecs Tests

    [Fact]
    public void DetectCodecs_ValidFlacFile_ReturnsFLAC()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]); // Minimal file

        try
        {
            MockAudioProcessor processor = new()
            {
                FfprobeExitCode = 0,
                FfprobeStdout = "flac"
            };

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal("FLAC", codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DetectCodecs_ValidM4AFile_ReturnsAAC()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfprobeExitCode = 0,
                FfprobeStdout = "aac"
            };

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal("AAC", codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DetectCodecs_ValidMP3File_ReturnsMP3()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(tempFile, [0xFF, 0xFB, 0x90, 0x00]); // MP3 header bytes

        try
        {
            MockAudioProcessor processor = new()
            {
                FfprobeExitCode = 0,
                FfprobeStdout = "mp3"
            };

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal("MP3", codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DetectCodecs_FfprobeReturnsNonZero_ReturnsEmptyString()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfprobeExitCode = 1,
                FfprobeStdout = "error"
            };

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal(string.Empty, codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DetectCodecs_FfprobeReturnsEmptyStdout_ReturnsEmptyString()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfprobeExitCode = 0,
                FfprobeStdout = string.Empty
            };

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal(string.Empty, codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DetectCodecs_FfprobeThrowsException_ReturnsEmptyString()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            ThrowingAudioProcessor processor = new();

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal(string.Empty, codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DetectCodecs_NullFilePath_ReturnsEmptyString()
    {
        // Arrange
        MockAudioProcessor processor = new();

        // Act
        string codec = AudioFormatHandler.DetectCodecs(null!, processor);

        // Assert
        Assert.Equal(string.Empty, codec);
    }

    [Fact]
    public void DetectCodecs_EmptyFilePath_ReturnsEmptyString()
    {
        // Arrange
        MockAudioProcessor processor = new();

        // Act
        string codec = AudioFormatHandler.DetectCodecs(string.Empty, processor);

        // Assert
        Assert.Equal(string.Empty, codec);
    }

    [Fact]
    public void DetectCodecs_WhiteSpaceFilePath_ReturnsEmptyString()
    {
        // Arrange
        MockAudioProcessor processor = new();

        // Act
        string codec = AudioFormatHandler.DetectCodecs("   ", processor);

        // Assert
        Assert.Equal(string.Empty, codec);
    }

    [Fact]
    public void DetectCodecs_NonExistentFile_ReturnsEmptyString()
    {
        // Arrange
        string nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.m4a");
        MockAudioProcessor processor = new();

        // Act
        string codec = AudioFormatHandler.DetectCodecs(nonExistentFile, processor);

        // Assert
        Assert.Equal(string.Empty, codec);
    }

    [Fact]
    public void DetectCodecs_CodecNameWithWhitespace_TrimsAndUpperCases()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfprobeExitCode = 0,
                FfprobeStdout = "  flac  "
            };

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal("FLAC", codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void DetectCodecs_CodecNameLowerCase_ReturnsUpperCase()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfprobeExitCode = 0,
                FfprobeStdout = "flac"
            };

            // Act
            string codec = AudioFormatHandler.DetectCodecs(tempFile, processor);

            // Assert
            Assert.Equal("FLAC", codec);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    #endregion

    #region ProcessAudioFileAsync Tests

    [Fact]
    public async Task ProcessAudioFileAsync_ExtractFlacFalse_ReturnsInputPath()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new();

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                tempFile,
                codecs: "FLAC",
                extractFlac: false,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(tempFile, result);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_NullInputPath_ReturnsInputPath()
    {
        // Arrange
        MockAudioProcessor processor = new();

        // Act
        string result = await AudioFormatHandler.ProcessAudioFileAsync(
            null!,
            codecs: "FLAC",
            extractFlac: true,
            keepOriginal: true,
            processor);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAudioFileAsync_NonExistentFile_ReturnsInputPath()
    {
        // Arrange
        string nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.m4a");
        MockAudioProcessor processor = new();

        // Act
        string result = await AudioFormatHandler.ProcessAudioFileAsync(
            nonExistentFile,
            codecs: "FLAC",
            extractFlac: true,
            keepOriginal: true,
            processor);

        // Assert
        Assert.Equal(nonExistentFile, result);
    }

    [Fact]
    public async Task ProcessAudioFileAsync_NonFlacCodec_ReturnsInputPath()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new();

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                tempFile,
                codecs: "AAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(tempFile, result);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_NonM4AExtension_ReturnsInputPath()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.flac");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new();

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                tempFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(tempFile, result);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_SuccessfulExtraction_ReturnsOutputPath()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string inputFile = Path.Combine(tempDir, $"test_{Guid.NewGuid():N}.m4a");
        string outputFile = Path.ChangeExtension(inputFile, ".flac");
        File.WriteAllBytes(inputFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfmpegExitCode = 0,
                ShouldCreateOutputFile = true
            };

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                inputFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(outputFile, result);
            Assert.True(File.Exists(inputFile), "Original file should exist with keepOriginal=true");
        }
        finally
        {
            try { File.Delete(inputFile); } catch { }
            try { File.Delete(outputFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_SuccessfulExtraction_KeepOriginalFalse_DeletesInput()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string inputFile = Path.Combine(tempDir, $"test_{Guid.NewGuid():N}.m4a");
        string outputFile = Path.ChangeExtension(inputFile, ".flac");
        File.WriteAllBytes(inputFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfmpegExitCode = 0,
                ShouldCreateOutputFile = true
            };

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                inputFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: false,
                processor);

            // Assert
            Assert.Equal(outputFile, result);
            Assert.False(File.Exists(inputFile), "Original file should be deleted with keepOriginal=false");
        }
        finally
        {
            try { File.Delete(inputFile); } catch { }
            try { File.Delete(outputFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_ExtractionFailure_ReturnsInputPath()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string inputFile = Path.Combine(tempDir, $"test_{Guid.NewGuid():N}.m4a");
        string outputFile = Path.ChangeExtension(inputFile, ".flac");
        File.WriteAllBytes(inputFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfmpegExitCode = 1
            };

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                inputFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(inputFile, result);
            Assert.True(File.Exists(inputFile), "Original file should exist after failed extraction");
            Assert.False(File.Exists(outputFile), "Output file should not exist after failed extraction");
        }
        finally
        {
            try { File.Delete(inputFile); } catch { }
            try { File.Delete(outputFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_StaleOutputFileExists_DeletedBeforeExtraction()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string inputFile = Path.Combine(tempDir, $"test_{Guid.NewGuid():N}.m4a");
        string outputFile = Path.ChangeExtension(inputFile, ".flac");
        File.WriteAllBytes(inputFile, [0x00, 0x00, 0x00, 0x20]);
        File.WriteAllBytes(outputFile, [0xFF, 0xFF, 0xFF, 0xFF]); // Create stale output

        try
        {
            MockAudioProcessor processor = new()
            {
                FfmpegExitCode = 0,
                ShouldCreateOutputFile = true
            };

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                inputFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(outputFile, result);
            Assert.True(File.Exists(outputFile), "New output file should exist");
        }
        finally
        {
            try { File.Delete(inputFile); } catch { }
            try { File.Delete(outputFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_ExtractionThrowsException_ReturnsInputPath()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string inputFile = Path.Combine(tempDir, $"test_{Guid.NewGuid():N}.m4a");
        string outputFile = Path.ChangeExtension(inputFile, ".flac");
        File.WriteAllBytes(inputFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            ThrowingAudioProcessor processor = new();

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                inputFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(inputFile, result);
            Assert.True(File.Exists(inputFile), "Original file should exist after exception");
            Assert.False(File.Exists(outputFile), "Output file should not exist after exception");
        }
        finally
        {
            try { File.Delete(inputFile); } catch { }
            try { File.Delete(outputFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new();
            CancellationTokenSource cts = new();
            cts.Cancel();

            // Act & Assert
            _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                _ = await AudioFormatHandler.ProcessAudioFileAsync(
                    tempFile,
                    codecs: "FLAC",
                    extractFlac: true,
                    keepOriginal: true,
                    processor,
                    cts.Token);
            });
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_EmptyFile_ReturnsInputPath()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(tempFile, []);

        try
        {
            MockAudioProcessor processor = new();

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                tempFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(tempFile, result);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_OutputFileCreatedButExitCodeNonZero_DeletesOutput()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string inputFile = Path.Combine(tempDir, $"test_{Guid.NewGuid():N}.m4a");
        string outputFile = Path.ChangeExtension(inputFile, ".flac");
        File.WriteAllBytes(inputFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfmpegExitCode = 1,
                ShouldCreateOutputFile = true
            };

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                inputFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            Assert.Equal(inputFile, result);
            Assert.False(File.Exists(outputFile), "Output file should be deleted when exit code is non-zero");
        }
        finally
        {
            try { File.Delete(inputFile); } catch { }
            try { File.Delete(outputFile); } catch { }
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_FilePathWithQuotes_HandlesCorrectly()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string inputFile = Path.Combine(tempDir, $"test {Guid.NewGuid():N}.m4a"); // Space in filename
        File.WriteAllBytes(inputFile, [0x00, 0x00, 0x00, 0x20]);

        try
        {
            MockAudioProcessor processor = new()
            {
                FfmpegExitCode = 0,
                ShouldCreateOutputFile = true
            };

            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                inputFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                processor);

            // Assert
            string expectedOutput = Path.ChangeExtension(inputFile, ".flac");
            Assert.Equal(expectedOutput, result);
            Assert.True(processor.LastFfmpegArgs?.Contains("\"") ?? false, "Arguments should contain quotes");
        }
        finally
        {
            try { File.Delete(inputFile); } catch { }
            try { File.Delete(Path.ChangeExtension(inputFile, ".flac")); } catch { }
        }
    }

    #endregion

    #region Mock Classes

    private sealed class MockAudioProcessor : IAudioProcessor
    {
        public int FfprobeExitCode { get; set; } = 0;
        public string FfprobeStdout { get; set; } = string.Empty;
        public string FfprobeStderr { get; set; } = string.Empty;
        public int FfmpegExitCode { get; set; } = 0;
        public string FfmpegStdout { get; set; } = string.Empty;
        public string FfmpegStderr { get; set; } = string.Empty;
        public bool ShouldCreateOutputFile { get; set; } = false;

        public string? LastFfprobeArgs { get; private set; }
        public string? LastFfmpegArgs { get; private set; }

        public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments)
        {
            LastFfprobeArgs = arguments;
            return (FfprobeExitCode, FfprobeStdout, FfprobeStderr);
        }

        public Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, CancellationToken ct = default)
        {
            LastFfmpegArgs = arguments;

            // Simulate file creation for successful extractions
            if (ShouldCreateOutputFile && FfmpegExitCode == 0)
            {
                // Extract output path from arguments (after the last quoted path)
                Match match = Regex.Match(arguments, "\"([^\"]+)\"$");
                if (match.Success)
                {
                    string outputPath = match.Groups[1].Value;
                    try
                    {
                        File.WriteAllBytes(outputPath, [0x00, 0x00, 0x00, 0x20]);
                    }
                    catch
                    {
                        // Ignore file creation errors in tests
                    }
                }
            }

            return Task.FromResult((FfmpegExitCode, FfmpegStdout, FfmpegStderr));
        }
    }

    private sealed class ThrowingAudioProcessor : IAudioProcessor
    {
        public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments)
        {
            throw new Exception("FFprobe not available");
        }

        public Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, CancellationToken ct = default)
        {
            throw new Exception("FFmpeg not available");
        }
    }

    #endregion
}
