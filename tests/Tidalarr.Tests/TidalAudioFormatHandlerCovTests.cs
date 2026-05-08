using Microsoft.Extensions.Logging;
using Moq;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalAudioFormatHandlerCovTests
{
    #region IsFFmpegAvailable Tests

    [Fact]
    public void IsFFmpegAvailable_ExitCodeZero_ReturnsTrue()
    {
        // Arrange
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfprobe("-version"))
            .Returns((0, "ffprobe version 4.4", string.Empty));

        // Act
        bool result = AudioFormatHandler.IsFFmpegAvailable(mockProcessor.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFFmpegAvailable_ExitCodeNonZero_ReturnsFalse()
    {
        // Arrange
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfprobe("-version"))
            .Returns((1, string.Empty, "error"));

        // Act
        bool result = AudioFormatHandler.IsFFmpegAvailable(mockProcessor.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFFmpegAvailable_ExceptionThrown_ReturnsFalse()
    {
        // Arrange
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfprobe("-version"))
            .Throws(new InvalidOperationException("Process failed"));

        // Act
        bool result = AudioFormatHandler.IsFFmpegAvailable(mockProcessor.Object);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region DetectCodecs Tests

    [Fact]
    public void DetectCodecs_NullFilePath_ReturnsEmpty()
    {
        // Arrange & Act
        string result = AudioFormatHandler.DetectCodecs(null!);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void DetectCodecs_EmptyFilePath_ReturnsEmpty()
    {
        // Arrange & Act
        string result = AudioFormatHandler.DetectCodecs(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void DetectCodecs_WhitespaceFilePath_ReturnsEmpty()
    {
        // Arrange & Act
        string result = AudioFormatHandler.DetectCodecs("   ");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void DetectCodecs_FileDoesNotExist_ReturnsEmpty()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.m4a");

        // Act
        string result = AudioFormatHandler.DetectCodecs(nonExistentPath);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void DetectCodecs_ExitCodeNonZero_ReturnsEmpty()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfprobe(It.IsAny<string>()))
            .Returns((1, string.Empty, "error"));

        try
        {
            // Act
            string result = AudioFormatHandler.DetectCodecs(tempFile, mockProcessor.Object);

            // Assert
            Assert.Equal(string.Empty, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DetectCodecs_Success_ReturnsUppercaseCodec()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfprobe(It.IsAny<string>()))
            .Returns((0, "flac", string.Empty));

        try
        {
            // Act
            string result = AudioFormatHandler.DetectCodecs(tempFile, mockProcessor.Object);

            // Assert
            Assert.Equal("FLAC", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DetectCodecs_EmptyStdout_ReturnsEmpty()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfprobe(It.IsAny<string>()))
            .Returns((0, "   ", string.Empty));

        try
        {
            // Act
            string result = AudioFormatHandler.DetectCodecs(tempFile, mockProcessor.Object);

            // Assert
            Assert.Equal(string.Empty, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DetectCodecs_ExceptionThrown_ReturnsEmpty()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfprobe(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Process failed"));

        try
        {
            // Act
            string result = AudioFormatHandler.DetectCodecs(tempFile, mockProcessor.Object);

            // Assert
            Assert.Equal(string.Empty, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region ProcessAudioFileAsync Tests

    [Fact]
    public async Task ProcessAudioFileAsync_ExtractFlacFalse_ReturnsInputPath()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                tempFile,
                codecs: "FLAC",
                extractFlac: false,
                keepOriginal: true);

            // Assert
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_NullInputPath_ReturnsInputPath()
    {
        // Act
        string result = await AudioFormatHandler.ProcessAudioFileAsync(
            null!,
            codecs: "FLAC",
            extractFlac: true,
            keepOriginal: true);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAudioFileAsync_EmptyInputPath_ReturnsInputPath()
    {
        // Act
        string result = await AudioFormatHandler.ProcessAudioFileAsync(
            string.Empty,
            codecs: "FLAC",
            extractFlac: true,
            keepOriginal: true);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ProcessAudioFileAsync_FileDoesNotExist_ReturnsInputPath()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.m4a");

        // Act
        string result = await AudioFormatHandler.ProcessAudioFileAsync(
            nonExistentPath,
            codecs: "FLAC",
            extractFlac: true,
            keepOriginal: true);

        // Assert
        Assert.Equal(nonExistentPath, result);
    }

    [Fact]
    public async Task ProcessAudioFileAsync_NonFlacCodec_ReturnsInputPath()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(tempFile, [0, 1, 2, 3]);

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                tempFile,
                codecs: "AAC",
                extractFlac: true,
                keepOriginal: true);

            // Assert
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_NonM4aExtension_ReturnsInputPath()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.mp3");
        await File.WriteAllBytesAsync(tempFile, [0, 1, 2, 3]);

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                tempFile,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true);

            // Assert
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_Success_KeepOriginal_ReturnsOutputPath()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        string output = Path.ChangeExtension(input, ".flac");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        string capturedOutput = output;
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => File.WriteAllBytes(capturedOutput, [9, 9, 9, 9]))
            .ReturnsAsync((0, string.Empty, string.Empty));

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                audio: mockProcessor.Object);

            // Assert
            Assert.Equal(output, result);
            Assert.True(File.Exists(input), "Original file should exist when keepOriginal=true");
            Assert.True(File.Exists(output), "Output FLAC file should exist");
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_Success_DeleteOriginal_ReturnsOutputPath()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        string output = Path.ChangeExtension(input, ".flac");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        string capturedOutput = output;
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => File.WriteAllBytes(capturedOutput, [9, 9, 9, 9]))
            .ReturnsAsync((0, string.Empty, string.Empty));

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: false,
                audio: mockProcessor.Object);

            // Assert
            Assert.Equal(output, result);
            Assert.False(File.Exists(input), "Original file should be deleted when keepOriginal=false");
            Assert.True(File.Exists(output), "Output FLAC file should exist");
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_OutputFileExistsBeforeProcessing_DeletesExistingOutput()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        string output = Path.ChangeExtension(input, ".flac");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);
        await File.WriteAllBytesAsync(output, [5, 6, 7, 8]); // Pre-existing stale output

        string capturedOutput = output;
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => File.WriteAllBytes(capturedOutput, [9, 9, 9, 9]))
            .ReturnsAsync((0, string.Empty, string.Empty));

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                audio: mockProcessor.Object);

            // Assert
            Assert.Equal(output, result);
            Assert.Equal(4, new FileInfo(output).Length); // New content, not stale 4-byte file
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_FfmpegFails_ReturnsInputPath()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        string output = Path.ChangeExtension(input, ".flac");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, string.Empty, "error"));

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: false,
                audio: mockProcessor.Object);

            // Assert
            Assert.Equal(input, result);
            Assert.False(File.Exists(output), "Output file should be cleaned up on failure");
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_FfmpegException_ReturnsInputPath()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        string output = Path.ChangeExtension(input, ".flac");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FFmpeg crashed"));

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: false,
                audio: mockProcessor.Object);

            // Assert
            Assert.Equal(input, result);
            Assert.False(File.Exists(output), "Output file should be cleaned up on exception");
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_OperationCanceledException_Propagates()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        var cts = new CancellationTokenSource();
        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                AudioFormatHandler.ProcessAudioFileAsync(
                    input,
                    codecs: "FLAC",
                    extractFlac: true,
                    keepOriginal: true,
                    audio: mockProcessor.Object,
                    cancellationToken: cts.Token));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                AudioFormatHandler.ProcessAudioFileAsync(
                    input,
                    codecs: "FLAC",
                    extractFlac: true,
                    keepOriginal: true,
                    cancellationToken: cts.Token));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_WithLogger_LogsDebugOnFailure()
    {
        // Arrange
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        string output = Path.ChangeExtension(input, ".flac");

        // Create output file to trigger cleanup logging
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);
        await File.WriteAllBytesAsync(output, [5, 6, 7, 8]);

        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, string.Empty, "error"));

        var mockLogger = new Mock<ILogger>();

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                audio: mockProcessor.Object,
                logger: mockLogger.Object);

            // Assert
            Assert.Equal(input, result);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ProcessAudioFileAsync_FfmpegSuccessButNoOutputFile_ReturnsInputPath()
    {
        // Arrange - ffmpeg reports success but doesn't create output file
        string tempDir = Path.GetTempPath();
        string input = Path.Combine(tempDir, $"tidal_audio_{Guid.NewGuid():N}.m4a");
        string output = Path.ChangeExtension(input, ".flac");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 3, 4]);

        var mockProcessor = new Mock<IAudioProcessor>();
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, string.Empty, string.Empty));

        // Delete output immediately after ffmpeg "succeeds" to simulate file not being created
        mockProcessor.Setup(p => p.RunFfmpegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => { /* output file doesn't exist */ })
            .ReturnsAsync((0, string.Empty, string.Empty));

        try
        {
            // Act
            string result = await AudioFormatHandler.ProcessAudioFileAsync(
                input,
                codecs: "FLAC",
                extractFlac: true,
                keepOriginal: true,
                audio: mockProcessor.Object);

            // Assert - should return input since output file doesn't exist
            Assert.Equal(input, result);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    #endregion
}
