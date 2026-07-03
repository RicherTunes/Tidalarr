using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.HostBridge;
using NLog;
using Tidalarr.Application.Services;
using Tidalarr.Core.Exceptions;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// The download-client side of terminal-release suppression: after a failed album download, if a
/// permanent (terminal) per-track restriction was observed, record the album id in the store. Crucially,
/// this is a pure search-side side effect — it does NOT change the album-completion contract (an incomplete
/// album is still reported to Lidarr as Failed).
/// </summary>
public sealed class TidalTerminalSuppressionRecordingTests
{
    [Fact]
    public async Task TryRecord_WithTerminalRestriction_SuppressesAlbumId()
    {
        var store = new RecordingStore();
        var terminals = new[] { new TidalTerminalRestriction("track-7", TidalStreamUnavailableReason.RightsRemoved) };

        await TidalLidarrDownloadClient.TryRecordTerminalReleaseSuppressionAsync(
            store, "album-42", terminals, LogManager.GetCurrentClassLogger());

        Assert.Equal("album-42", store.LastAlbumId);
        Assert.Equal("track-7", store.LastTrackId);
        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, store.LastReason);
    }

    [Fact]
    public async Task TryRecord_WithNoTerminalRestriction_DoesNotSuppress()
    {
        var store = new RecordingStore();

        await TidalLidarrDownloadClient.TryRecordTerminalReleaseSuppressionAsync(
            store, "album-42", Array.Empty<TidalTerminalRestriction>(), LogManager.GetCurrentClassLogger());

        Assert.Null(store.LastAlbumId);
    }

    [Fact]
    public async Task TryRecord_WithBlankAlbumId_DoesNotSuppress()
    {
        var store = new RecordingStore();
        var terminals = new[] { new TidalTerminalRestriction("track-7", TidalStreamUnavailableReason.RightsRemoved) };

        await TidalLidarrDownloadClient.TryRecordTerminalReleaseSuppressionAsync(
            store, "  ", terminals, LogManager.GetCurrentClassLogger());

        Assert.Null(store.LastAlbumId);
    }

    [Fact]
    public async Task TryRecord_StoreThrows_DoesNotPropagate()
    {
        // Suppression is best-effort: a store failure must never mask / replace the original download
        // failure the caller is already reporting.
        var store = new ThrowingStore();
        var terminals = new[] { new TidalTerminalRestriction("track-7", TidalStreamUnavailableReason.RightsRemoved) };

        var ex = await Record.ExceptionAsync(() => TidalLidarrDownloadClient.TryRecordTerminalReleaseSuppressionAsync(
            store, "album-42", terminals, LogManager.GetCurrentClassLogger()));

        Assert.Null(ex);
    }

    // Completion contract: an incomplete album (Failed host-bridge status) still projects to Lidarr's
    // Failed status. Suppression is search-side only and must not soften this to Completed.
    [Fact]
    public void ProjectDownloadItems_FailedAlbum_StillReportsFailed()
    {
        var item = new HostBridgeDownloadItem
        {
            DownloadId = "dl-1",
            AlbumId = "album-42",
            Title = "Album",
            Artist = "Artist",
            OutputPath = "/x",
        };
        item.SetStatus(HostBridgeDownloadItemStatus.Failed);

        var projected = TidalLidarrDownloadClient.ProjectDownloadItems(new[] { item }, clientInfo: null);

        Assert.Single(projected);
        Assert.Equal(NzbDrone.Core.Download.DownloadItemStatus.Failed, projected[0].Status);
    }

    private sealed class RecordingStore : ITidalReleaseSuppressionStore
    {
        public string? LastAlbumId { get; private set; }
        public string? LastTrackId { get; private set; }
        public TidalStreamUnavailableReason? LastReason { get; private set; }

        public bool IsSuppressed(string albumId) => false;

        public Task SuppressAsync(string albumId, string trackId, TidalStreamUnavailableReason reason, CancellationToken cancellationToken = default)
        {
            LastAlbumId = albumId;
            LastTrackId = trackId;
            LastReason = reason;
            return Task.CompletedTask;
        }

        public Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class ThrowingStore : ITidalReleaseSuppressionStore
    {
        public bool IsSuppressed(string albumId) => false;
        public Task SuppressAsync(string albumId, string trackId, TidalStreamUnavailableReason reason, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("store down");
        public Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
