using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Xunit;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

public sealed class TidalDownloadClientFinalizationTests
{
    private sealed class NonSeekableReadStream(byte[] payload) : Stream
    {
        private readonly byte[] _payload = payload ?? throw new ArgumentNullException(nameof(payload));
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (_position >= _payload.Length)
            {
                return 0;
            }

            int toCopy = Math.Min(count, _payload.Length - _position);
            Array.Copy(_payload, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_position >= _payload.Length)
            {
                return ValueTask.FromResult(0);
            }

            int toCopy = Math.Min(buffer.Length, _payload.Length - _position);
            _payload.AsSpan(_position, toCopy).CopyTo(buffer.Span);
            _position += toCopy;
            return ValueTask.FromResult(toCopy);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FakeAudioFormatHandler : IAudioFormatHandler
    {
        public int CallCount { get; private set; }
        public string? LastInputPath { get; private set; }
        public string? LastCodecs { get; private set; }
        public bool? LastExtractFlac { get; private set; }
        public bool? LastKeepOriginal { get; private set; }

        public Task<string> ProcessAudioFileAsync(
            string inputPath,
            string codecs,
            bool extractFlac = true,
            bool keepOriginal = false,
            IAudioProcessor? audio = null)
        {
            CallCount++;
            LastInputPath = inputPath;
            LastCodecs = codecs;
            LastExtractFlac = extractFlac;
            LastKeepOriginal = keepOriginal;

            string flacPath = Path.ChangeExtension(inputPath, "flac");
            File.WriteAllBytes(flacPath, Encoding.ASCII.GetBytes("fLaC").Concat(Enumerable.Repeat((byte)0x00, 2048)).ToArray());

            if (!keepOriginal && File.Exists(inputPath))
            {
                File.Delete(inputPath);
            }

            return Task.FromResult(flacPath);
        }
    }

    private sealed class TestableTidalDownloadClient : TidalDownloadClient
    {
        public bool MetadataApplied { get; private set; }
        public string? MetadataAppliedToPath { get; private set; }

        public TestableTidalDownloadClient(TidalDownloadClientSettings settings, IAudioFormatHandler audioFormatHandler)
            : base(
                streamService: null!,
                chunkDownloader: null!,
                apiClient: null!,
                qualityDetector: new TidalQualityDetector(),
                settings: settings,
                logger: null,
                audioFormatHandler: audioFormatHandler)
        {
        }

        protected override Task ApplyMetadataTagsAsync(string filePath, StreamingTrack metadata)
        {
            MetadataApplied = true;
            MetadataAppliedToPath = filePath;
            return Task.CompletedTask;
        }

        public Task<string> FinalizeForTestAsync(
            Stream audioStream,
            string outputPath,
            string? payloadFileExtension,
            string? mimeType,
            StreamingTrack track,
            string? tempFileExtension = null,
            bool extractFlac = false,
            string? codec = null,
            CancellationToken cancellationToken = default)
        {
            return FinalizeDownloadedTrackAsync(
                audioStream,
                outputPath,
                payloadFileExtension,
                mimeType,
                track,
                cancellationToken,
                tempFileExtension: tempFileExtension,
                extractFlac: extractFlac,
                codec: codec);
        }
    }

    [Fact]
    public async Task FinalizeDownloadedTrackAsync_WithFlacPayload_WritesFileAndAppliesMetadata()
    {
        var settings = new TidalDownloadClientSettings
        {
            DownloadPath = Path.GetTempPath(),
            PreferredQuality = TidalQuality.Lossless,
            ExtractFlac = true
        };
        var audioFormatHandler = new FakeAudioFormatHandler();
        var client = new TestableTidalDownloadClient(settings, audioFormatHandler);

        var tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var outputPath = Path.Combine(tempDir, "01 - Test Track.flac");
            var payload = Encoding.ASCII.GetBytes("fLaC").Concat(Enumerable.Repeat((byte)0x00, 2048)).ToArray();

            using var audioStream = new MemoryStream(payload);
            var track = new StreamingTrack { Title = "Test Track", TrackNumber = 1 };

            var finalPath = await client.FinalizeForTestAsync(
                audioStream,
                outputPath,
                payloadFileExtension: ".flac",
                mimeType: "audio/flac",
                track: track);

            Assert.True(File.Exists(finalPath));
            Assert.False(File.Exists(finalPath + ".partial"));
            Assert.True(client.MetadataApplied);
            Assert.Equal(finalPath, client.MetadataAppliedToPath);
            Assert.Equal(0, audioFormatHandler.CallCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task FinalizeDownloadedTrackAsync_WithNonSeekableStream_DoesNotRequireSeeking()
    {
        var settings = new TidalDownloadClientSettings
        {
            DownloadPath = Path.GetTempPath(),
            PreferredQuality = TidalQuality.Lossless,
            ExtractFlac = true
        };
        var audioFormatHandler = new FakeAudioFormatHandler();
        var client = new TestableTidalDownloadClient(settings, audioFormatHandler);

        var tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var outputPath = Path.Combine(tempDir, "01 - Test Track.flac");
            var payload = Encoding.ASCII.GetBytes("fLaC").Concat(Enumerable.Repeat((byte)0x00, 2048)).ToArray();

            using Stream audioStream = new NonSeekableReadStream(payload);
            var track = new StreamingTrack { Title = "Test Track", TrackNumber = 1 };

            var finalPath = await client.FinalizeForTestAsync(
                audioStream,
                outputPath,
                payloadFileExtension: ".flac",
                mimeType: "audio/flac",
                track: track);

            Assert.True(File.Exists(finalPath));
            Assert.False(File.Exists(finalPath + ".partial"));
            Assert.True(client.MetadataApplied);
            Assert.Equal(finalPath, client.MetadataAppliedToPath);
            Assert.Equal(0, audioFormatHandler.CallCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task FinalizeDownloadedTrackAsync_WithM4aPayload_WritesFileAndAppliesMetadata()
    {
        var settings = new TidalDownloadClientSettings
        {
            DownloadPath = Path.GetTempPath(),
            PreferredQuality = TidalQuality.Lossless,
            ExtractFlac = true
        };
        var audioFormatHandler = new FakeAudioFormatHandler();
        var client = new TestableTidalDownloadClient(settings, audioFormatHandler);

        var tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var outputPath = Path.Combine(tempDir, "01 - Test Track.m4a");
            var tempPartialPath = outputPath + ".partial";

            var payload = new byte[]
            {
                0x00, 0x00, 0x00, 0x18, // box size
                (byte)'f', (byte)'t', (byte)'y', (byte)'p',
                (byte)'i', (byte)'s', (byte)'o', (byte)'m',
                0x00, 0x00, 0x00, 0x00
            };

            using var audioStream = new MemoryStream(payload.Concat(Enumerable.Repeat((byte)0x01, 2048)).ToArray());
            var track = new StreamingTrack { Title = "Test Track", TrackNumber = 1 };

            var finalPath = await client.FinalizeForTestAsync(
                audioStream,
                outputPath,
                payloadFileExtension: ".m4a",
                mimeType: "audio/mp4",
                track: track,
                tempFileExtension: ".m4a");

            Assert.Equal(outputPath, finalPath);
            Assert.True(File.Exists(finalPath));
            Assert.False(File.Exists(tempPartialPath));
            Assert.True(client.MetadataApplied);
            Assert.Equal(finalPath, client.MetadataAppliedToPath);
            Assert.Equal(0, audioFormatHandler.CallCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task FinalizeDownloadedTrackAsync_WithM4aPayloadAndExtractFlac_UsesAudioFormatHandler()
    {
        var settings = new TidalDownloadClientSettings
        {
            DownloadPath = Path.GetTempPath(),
            PreferredQuality = TidalQuality.Lossless,
            ExtractFlac = true
        };
        var audioFormatHandler = new FakeAudioFormatHandler();
        var client = new TestableTidalDownloadClient(settings, audioFormatHandler);

        var tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var outputPath = Path.Combine(tempDir, "01 - Test Track.flac");
            var tempM4aPath = Path.ChangeExtension(outputPath, ".m4a");
            var tempM4aPartialPath = tempM4aPath + ".partial";

            var payload = new byte[]
            {
                0x00, 0x00, 0x00, 0x18, // box size
                (byte)'f', (byte)'t', (byte)'y', (byte)'p',
                (byte)'i', (byte)'s', (byte)'o', (byte)'m',
                0x00, 0x00, 0x00, 0x00
            };

            using var audioStream = new MemoryStream(payload.Concat(Enumerable.Repeat((byte)0x01, 2048)).ToArray());
            var track = new StreamingTrack { Title = "Test Track", TrackNumber = 1 };

            var finalPath = await client.FinalizeForTestAsync(
                audioStream,
                outputPath,
                payloadFileExtension: ".m4a",
                mimeType: "audio/mp4",
                track: track,
                tempFileExtension: ".m4a",
                extractFlac: true,
                codec: "FLAC");

            Assert.Equal(outputPath, finalPath);
            Assert.True(File.Exists(finalPath));
            Assert.False(File.Exists(tempM4aPartialPath));
            Assert.False(File.Exists(tempM4aPath));
            Assert.True(client.MetadataApplied);
            Assert.Equal(finalPath, client.MetadataAppliedToPath);

            Assert.Equal(1, audioFormatHandler.CallCount);
            Assert.Equal(tempM4aPath, audioFormatHandler.LastInputPath);
            Assert.Equal("FLAC", audioFormatHandler.LastCodecs);
            Assert.True(audioFormatHandler.LastExtractFlac);
            Assert.False(audioFormatHandler.LastKeepOriginal);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
