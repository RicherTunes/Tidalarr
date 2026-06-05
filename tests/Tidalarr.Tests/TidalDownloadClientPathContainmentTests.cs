using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

/// <summary>
/// F-09: the public download write boundaries must refuse a caller-provided outputPath that resolves
/// outside the configured DownloadPath (the system temp dir is also allowed — DownloadTrackAsync stages
/// there before the host imports). Containment is the canonical-form check from Common's PathTraversalGuard.
/// </summary>
public class TidalDownloadClientPathContainmentTests
{
    // ── Pure helper coverage (no client construction needed). ──────────────────────────────────────

    [Fact]
    public void IsOutputPathAllowed_UnderDownloadRoot_True()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tidal_root_{Guid.NewGuid():N}");
        var output = Path.Combine(root, "Artist", "Album", "01 Song.flac");

        Assert.True(TidalDownloadClient.IsOutputPathAllowed(output, root));
    }

    [Fact]
    public void IsOutputPathAllowed_UnderSystemTemp_True_EvenWhenRootDiffers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tidal_root_{Guid.NewGuid():N}");
        var tempStaged = Path.Combine(Path.GetTempPath(), $"tidalarr_{Guid.NewGuid():N}.flac");

        Assert.True(TidalDownloadClient.IsOutputPathAllowed(tempStaged, root));
    }

    [Fact]
    public void IsOutputPathAllowed_TraversalEscapingRoot_False()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tidal_root_{Guid.NewGuid():N}", "allowed");
        // Escape via "..": lands at AppContext.BaseDirectory, which is under neither root nor system temp.
        var escape = Path.Combine(root, "..", "..", "..", "..", "..",
            Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)), "x.flac");
        var resolved = Path.GetFullPath(escape);

        // Only assert rejection when the resolved path genuinely lands outside both allowed roots.
        if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
        {
            Assert.False(TidalDownloadClient.IsOutputPathAllowed(escape, root));
        }
    }

    [Fact]
    public void IsOutputPathAllowed_AbsolutePathOutsideBothRoots_False()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tidal_root_{Guid.NewGuid():N}");
        var outside = Path.Combine(AppContext.BaseDirectory, $"tidal_escape_{Guid.NewGuid():N}.flac");

        Assert.False(TidalDownloadClient.IsOutputPathAllowed(outside, root));
    }

    [Fact]
    public void IsOutputPathAllowed_Empty_False()
        => Assert.False(TidalDownloadClient.IsOutputPathAllowed("", Path.GetTempPath()));

    // ── The guard is wired into the public download methods. ───────────────────────────────────────

    private sealed class CoreStub : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken ct = default)
            => Task.FromResult(new TidalTrackInfo(trackId, "Song", ["Artist"], "al1", "Album", 1, 100, TidalQuality.Lossless, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken ct = default)
            => Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default)
            => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken ct = default)
            => GetAlbumAsync(albumId, ct);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken ct = default)
            => Task.FromResult(new TidalStreamInfo(trackId, ["https://chunk"], ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("network error");
    }

    private static TidalDownloadClient BuildClient(string downloadRoot)
    {
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = downloadRoot };
        var streamSvc = new TidalStreamService(new CoreStub(), new TidalManifestParser());
        var downloader = new TidalChunkDownloader(new HttpClient(new ThrowingHandler()));
        return new TidalDownloadClient(streamSvc, downloader, new CoreStub(), new TidalQualityDetector(), settings, NullLogger.Instance);
    }

    [Fact]
    public async Task DownloadTrackEnhancedAsync_OutputOutsideRoot_RefusedWithDownloadPathError()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tidal_root_{Guid.NewGuid():N}");
        var escape = Path.Combine(AppContext.BaseDirectory, $"tidal_escape_{Guid.NewGuid():N}.flac");
        var client = BuildClient(root);

        var res = await client.DownloadTrackEnhancedAsync("t1", escape, TidalQuality.Lossless);

        Assert.False(res.Success);
        Assert.Contains("download path", res.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(escape));
    }

    [Fact]
    public async Task DownloadTrackWithMetadataAsync_OutputOutsideRoot_RefusedWithDownloadPathError()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tidal_root_{Guid.NewGuid():N}");
        var escape = Path.Combine(AppContext.BaseDirectory, $"tidal_escape_{Guid.NewGuid():N}.flac");
        var client = BuildClient(root);

        var res = await client.DownloadTrackWithMetadataAsync("t1", escape, TidalQuality.Lossless);

        Assert.False(res.Success);
        Assert.Contains("download path", res.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(escape + ".partial"));
    }
}
