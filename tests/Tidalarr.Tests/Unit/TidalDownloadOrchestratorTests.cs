using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Application.Services;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// <see cref="TidalDownloadOrchestrator"/> adopts Common's <c>ValidateDownloadedPayload</c> seam with the
/// canonical <c>DownloadPayloadValidator</c>: a downloaded payload that is not plausible audio (an HTML
/// soft-404 served as 200, a JSON error document) must fail that TRACK — deleted from disk, never handed
/// to Lidarr's import as a fake .m4a/.flac. Real FLAC and M4A signatures must pass untouched.
/// </summary>
public sealed class TidalDownloadOrchestratorTests : IDisposable
{
    private readonly string _tempDir;

    public TidalDownloadOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TidalOrchTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private static TidalDownloadOrchestrator MakeOrchestrator(HttpMessageHandler handler, string extension)
    {
        return new TidalDownloadOrchestrator(
            serviceName: "Tidalarr",
            httpClient: new HttpClient(handler),
            getAlbumAsync: id => Task.FromResult(new StreamingAlbum { Id = id, Title = "A", Artist = new StreamingArtist { Name = "X" }, TrackCount = 1 }),
            getTrackAsync: id => Task.FromResult(new StreamingTrack { Id = id, Title = "T", TrackNumber = 1, Artist = new StreamingArtist { Name = "X" }, Album = new StreamingAlbum { Title = "A", Artist = new StreamingArtist { Name = "X" } } }),
            getAlbumTrackIdsAsync: _ => Task.FromResult((IReadOnlyList<string>)new List<string> { "t1" }),
            getStreamAsync: (id, q) => Task.FromResult(("https://93.184.216.34/stream", extension)),
            maxConcurrentTracks: 1,
            streamProvider: null,
            metadataApplier: new NoopMetadataApplier(),
            postProcessor: null,
            telemetrySink: null);
    }

    private static HttpResponseMessage Response(byte[] payload)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };

    private static byte[] HtmlPayload()
        => Encoding.UTF8.GetBytes("<!doctype html><html><body>403 Forbidden</body></html>" + new string('x', 4096));

    private static byte[] FlacPayload(int size = 4096)
    {
        var bytes = new byte[size];
        bytes[0] = (byte)'f'; bytes[1] = (byte)'L'; bytes[2] = (byte)'a'; bytes[3] = (byte)'C';
        return bytes;
    }

    private static byte[] M4aPayload(int size = 4096)
    {
        var bytes = new byte[size];
        // MP4/M4A signature: box size (4 bytes) then "ftyp" at offset 4.
        bytes[3] = 0x20;
        bytes[4] = (byte)'f'; bytes[5] = (byte)'t'; bytes[6] = (byte)'y'; bytes[7] = (byte)'p';
        return bytes;
    }

    [Fact]
    public async Task DownloadTrackAsync_HtmlPayloadServedAsM4a_FailsTrackAndDeletesFile()
    {
        var orch = MakeOrchestrator(new StubHandler(_ => Response(HtmlPayload())), extension: "m4a");
        var outPath = Path.Combine(_tempDir, "01 - T.m4a");

        var result = await orch.DownloadTrackAsync("t1", outPath, null, CancellationToken.None);

        result.Success.Should().BeFalse("an HTML soft-404 must never be imported as audio");
        File.Exists(outPath).Should().BeFalse("the rejected payload must be deleted");
    }

    [Fact]
    public async Task DownloadAlbumAsync_HtmlPayload_FailsAlbum()
    {
        var orch = MakeOrchestrator(new StubHandler(_ => Response(HtmlPayload())), extension: "flac");
        var outDir = Path.Combine(_tempDir, "album-html");

        var result = await orch.DownloadAlbumAsync("a1", outDir, null, null, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.TrackResults.Should().ContainSingle().Which.Success.Should().BeFalse();
        result.FilePaths.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadTrackAsync_RealFlacSignature_Succeeds()
    {
        var orch = MakeOrchestrator(new StubHandler(_ => Response(FlacPayload())), extension: "flac");
        var outPath = Path.Combine(_tempDir, "01 - T.flac");

        var result = await orch.DownloadTrackAsync("t1", outPath, null, CancellationToken.None);

        result.Success.Should().BeTrue($"a genuine FLAC payload must pass validation: {result.ErrorMessage}");
        File.Exists(result.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadTrackAsync_RealM4aSignature_Succeeds()
    {
        var orch = MakeOrchestrator(new StubHandler(_ => Response(M4aPayload())), extension: "m4a");
        var outPath = Path.Combine(_tempDir, "01 - T.m4a");

        var result = await orch.DownloadTrackAsync("t1", outPath, null, CancellationToken.None);

        result.Success.Should().BeTrue($"a genuine M4A payload must pass validation: {result.ErrorMessage}");
        File.Exists(result.FilePath).Should().BeTrue();
    }

    private sealed class NoopMetadataApplier : Lidarr.Plugin.Common.Interfaces.IAudioMetadataApplier
    {
        public Task ApplyAsync(string filePath, StreamingTrack metadata, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
